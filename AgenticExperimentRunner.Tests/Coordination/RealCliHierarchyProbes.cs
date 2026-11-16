using System.Text.Json;
using AgenticExperimentRunner.Coordination;
using AgenticExperimentRunner.RunPreparation;
using AgenticExperimentRunner.SessionExecution;
using Xunit;

/// <summary>
/// Prove con la Claude CLI reale: consumano chiamate LLM e vengono eseguite soltanto su
/// richiesta esplicita (<c>--explicit only</c>), mai dalla suite ordinaria.
/// </summary>
public sealed class RealCliHierarchyProbes
{
    private const string ClaudePath = @"C:\Users\Utente\.local\bin\claude.exe";
    private const string Model = "claude-sonnet-5-5";
    private static readonly TimeSpan DelegationDelay = TimeSpan.FromSeconds(150);

    private const string ProbeSpecification = """
        # Prova dell'harness (non è un task del benchmark)

        Questa è una prova tecnica del canale di delega. Chiama esattamente una volta lo
        strumento `delegate_task` con questo compito:

        "Leggi il file workspace/src/index.ts e rispondi indicando quante righe contiene."

        La chiamata può richiedere alcuni minuti: attendi la risposta. Poi termina
        riportando testualmente la risposta ricevuta. Non modificare alcun file.

        """;

    [Fact(Explicit = true)]
    public async Task LongDelegation_IsAwaitedByRealCoordinator()
    {
        using var repository = new TemporaryGitRepository();
        repository.WriteFile("BenchmarkTarget/src/index.ts", "export const valore = 1;\nexport const altro = 2;\n");
        repository.WriteFile("BenchmarkTarget/tests/public/common.test.ts", "// comune\n");
        repository.WriteFile("BenchmarkTarget/tests/public/t1/probe.test.ts", "// t1\n");
        repository.WriteFile("docs/benchmark/T1-specifica-pubblica.md", ProbeSpecification);
        repository.CommitAll("baseline di prova");
        string runId = $"a5-probe-long-delegation-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}";
        PrepareRunOutput prepared = await PrepareAsync(repository.RootPath, runId);

        var delayed = new List<DelayedDelegationHandler>();
        var runner = new HierarchicalRunRunner(TimeProvider.System, Path.Combine(AppContext.BaseDirectory, "Instructions"))
        {
            DelegationDecorator = inner =>
            {
                var handler = new DelayedDelegationHandler(inner, DelegationDelay);
                delayed.Add(handler);
                return handler;
            },
        };
        (HierarchicalRunRecord record, string recordPath) = await runner.RunAsync(
            new HierarchicalRunRequest(
                Path.Combine(prepared.ResultsDirectory, PrepareRunCommand.PreparedRunFileName),
                ClaudePath,
                Path.Combine(AppContext.BaseDirectory, "AgentWorkspaceMcpServer.exe"),
                RealToolchain(),
                Path.Combine(AppContext.BaseDirectory, "AgenticExperimentRunner.exe"),
                Model,
                TimeSpan.FromMinutes(15),
                300_000,
                10),
            TestContext.Current.CancellationToken);

        TestContext.Current.SendDiagnosticMessage($"record: {recordPath}");
        // Gli istanti dell'evento sono registrati dal gestore interno, dopo il ritardo: la durata
        // vista dal coordinatore si misura nel decoratore, dalla ricezione della richiesta.
        TimeSpan observed = Assert.Single(Assert.Single(delayed).Durations);
        TestContext.Current.SendDiagnosticMessage($"durata della delega vista dal relay: {observed}");
        Assert.True(observed >= DelegationDelay, $"durata {observed}");
        Assert.Equal(DelegationStatus.Completed, Assert.Single(record.Delegations).Status);
        Assert.Equal(SessionStatus.Completed, record.Status);

        // Il coordinatore deve ricevere come risultato dello strumento il riepilogo del lavoratore,
        // non una notifica di esecuzione in background.
        string workerSummary = record.Agents[1].Result!.ResultText!;
        string toolResult = Assert.Single(ToolResultTexts(
            Path.Combine(prepared.ResultsDirectory, "agents", "coordinator", "stream.jsonl")));
        Assert.Equal(workerSummary, toolResult);
    }

    private static McpDevelopmentToolchain RealToolchain()
    {
        string repositoryRoot = FindRepositoryRoot();
        string benchmark = Path.Combine(repositoryRoot, "BenchmarkTarget");
        string node = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, "node.exe"))
            .First(File.Exists);
        return new McpDevelopmentToolchain(
            node,
            Path.Combine(benchmark, "node_modules", "vitest", "vitest.mjs"),
            Path.Combine(benchmark, "node_modules", "typescript", "lib", "tsc.js"));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "BenchmarkTarget")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Root del repository non trovata.");
    }

    /// <summary>Testi dei <c>tool_result</c> ricevuti dall'agente, interpretando lo stream come JSON.</summary>
    private static IReadOnlyList<string> ToolResultTexts(string streamPath) =>
        File.ReadLines(streamPath)
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Where(root => root.TryGetProperty("type", out JsonElement type) && type.GetString() == "user")
            .SelectMany(root => root.GetProperty("message").GetProperty("content").EnumerateArray())
            .Where(block => block.ValueKind == JsonValueKind.Object
                && block.TryGetProperty("type", out JsonElement blockType) && blockType.GetString() == "tool_result")
            .Select(block => string.Concat(block.GetProperty("content").EnumerateArray().Select(part => part.GetProperty("text").GetString())))
            .ToList();

    private static async Task<PrepareRunOutput> PrepareAsync(string repositoryPath, string runId)
    {
        var command = new PrepareRunCommand(
            new RunAreaPreparer(new GitCommandRunner(), TimeProvider.System, TestDirectories.TempUserProfile()),
            TimeProvider.System);
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = await command.ExecuteAsync(
            [
                "--repository", repositoryPath, "--commit", "main", "--task", "T1",
                "--runs-root", @"C:\AgenticAIRuns", "--results-root", @"C:\AgenticAIResults", "--run-id", runId,
            ],
            output,
            error,
            TestContext.Current.CancellationToken);
        Assert.True(exitCode == 0, error.ToString());
        return JsonSerializer.Deserialize<PrepareRunOutput>(output.ToString(), PrepareRunOutput.SerializerOptions)!;
    }

    /// <summary>Ritarda ogni delega prima di passarla al gestore reale e ne misura la durata totale.</summary>
    private sealed class DelayedDelegationHandler(IDelegationHandler inner, TimeSpan delay) : IDelegationHandler
    {
        public List<TimeSpan> Durations { get; } = [];

        public async Task<DelegationOutcome> DelegateAsync(DelegationRequest request, CancellationToken cancellationToken)
        {
            long start = TimeProvider.System.GetTimestamp();
            await Task.Delay(delay, cancellationToken);
            DelegationOutcome outcome = await inner.DelegateAsync(request, cancellationToken);
            Durations.Add(TimeProvider.System.GetElapsedTime(start));
            return outcome;
        }
    }
}
