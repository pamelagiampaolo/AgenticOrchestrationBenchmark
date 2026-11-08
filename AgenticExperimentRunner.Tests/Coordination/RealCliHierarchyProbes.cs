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

        var runner = new HierarchicalRunRunner(TimeProvider.System, Path.Combine(AppContext.BaseDirectory, "Instructions"))
        {
            DelegationDecorator = inner => new DelayedDelegationHandler(inner, DelegationDelay),
        };
        (HierarchicalRunRecord record, string recordPath) = await runner.RunAsync(
            new HierarchicalRunRequest(
                Path.Combine(prepared.ResultsDirectory, PrepareRunCommand.PreparedRunFileName),
                ClaudePath,
                Path.Combine(AppContext.BaseDirectory, "McpReadOnlyPrototype.exe"),
                Path.Combine(AppContext.BaseDirectory, "AgenticExperimentRunner.exe"),
                Model,
                TimeSpan.FromMinutes(15),
                300_000,
                10),
            TestContext.Current.CancellationToken);

        TestContext.Current.SendDiagnosticMessage($"record: {recordPath}");
        DelegationEvent delegation = Assert.Single(record.Delegations);
        Assert.True(delegation.EndedAtUtc - delegation.RequestedAtUtc >= DelegationDelay);
        Assert.Equal(DelegationStatus.Completed, delegation.Status);
        Assert.Equal(SessionStatus.Completed, record.Status);
    }

    private static async Task<PrepareRunOutput> PrepareAsync(string repositoryPath, string runId)
    {
        var command = new PrepareRunCommand(
            new RunAreaPreparer(new GitCommandRunner(), TimeProvider.System, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
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

    /// <summary>Ritarda ogni delega prima di passarla al gestore reale.</summary>
    private sealed class DelayedDelegationHandler(IDelegationHandler inner, TimeSpan delay) : IDelegationHandler
    {
        public async Task<DelegationOutcome> DelegateAsync(DelegationRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return await inner.DelegateAsync(request, cancellationToken);
        }
    }
}
