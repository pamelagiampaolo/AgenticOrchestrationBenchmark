using System.Diagnostics;
using System.Text.Json;
using AgenticExperimentRunner.RunPreparation;
using AgenticExperimentRunner.SessionExecution;
using Xunit;

/// <summary>
/// Test di integrazione del runner con una CLI finta: nessuna chiamata LLM.
/// </summary>
public sealed class SingleAgentSessionRunnerTests : IDisposable
{
    private const string Specification = "# T1\n\nSpecifica con accenti: perché è così.\n";

    private readonly TemporaryGitRepository repository = new();
    private readonly string temporaryBase = Path.Combine(Path.GetTempPath(), $"a3-session-{Guid.NewGuid():N}");
    private readonly SingleAgentSessionRunner runner = new(
        TimeProvider.System, Path.Combine(AppContext.BaseDirectory, "Instructions"));

    public void Dispose()
    {
        repository.Dispose();
        TestDirectories.DeleteRecursively(temporaryBase);
    }

    [Fact]
    public async Task RunAsync_CompletedSession_RecordsUsageConfigurationAndInputs()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"),
        [
            FakeClaudeCli.Init(),
            FakeClaudeCli.Assistant("msg_1", 2, 100, 50, 1, toolUseId: "tool_1"),
            FakeClaudeCli.Assistant("msg_1", 2, 100, 50, 7, toolUseId: "tool_1"),
            FakeClaudeCli.ToolResult("tool_1", isError: true),
            FakeClaudeCli.Assistant("msg_2", 3, 150, 10, 20),
            "riga non json",
            FakeClaudeCli.Result(5, 250, 60, 27),
        ]);

        (SessionRecord record, string recordPath) = await runner.RunAsync(Request(prepared, cli), Token);

        Assert.Equal(SessionStatus.Completed, record.Status);
        Assert.Equal(Path.Combine(prepared.ResultsDirectory, "session-record.json"), recordPath);
        Assert.True(File.Exists(recordPath));
        Assert.Equal(new TokenUsage(5, 250, 60, 27), record.AuthoritativeUsage);
        Assert.Equal(new TokenUsage(5, 250, 60, 27), record.StreamUsage);
        Assert.True(record.StreamMatchesAuthoritativeUsage);
        Assert.Equal(342, record.TokensObserved);
        Assert.True(record.TokenMeasurementComplete);
        Assert.Null(record.TokenOverage);
        Assert.False(record.TerminatedByController);
        Assert.Empty(record.ConfigurationViolations);
        Assert.Equal(1, record.ToolCalls["mcp__benchmark_workspace__read_file"]);
        Assert.Equal(1, record.ToolErrorCount);
        Assert.Equal(1, record.MalformedLineCount);
        Assert.Equal("9.9.9 fake", record.CliVersion);
        Assert.Equal("9.9.9 fake", record.Toolchain.NodeVersion);
        Assert.Equal(GitTestProcess.Sha256(File.ReadAllBytes(cli.ExecutablePath)), record.Toolchain.NodeExecutableSha256);
        Assert.Equal("single-agent", record.Architecture);
        Assert.Equal(ExperimentRunKind.Pilot, record.RunKind);
        Assert.Equal(1, record.Repetition);
        Assert.Equal("test-seed", record.OrderSeed);
        Assert.Equal(1, record.OrderPosition);
        Assert.Equal(prepared.SourceCommit, record.SourceCommit);
        Assert.Empty(record.ProtectedIntegrityDifferences);
        Assert.Equal(
            Encoding().GetBytes(Specification),
            await File.ReadAllBytesAsync(cli.PromptPath, Token));
        Assert.Equal(
            GitTestProcess.Sha256(Encoding().GetBytes(Specification)),
            record.Hashes.TaskPrompt);
        Assert.Equal(7, File.ReadAllLines(Path.Combine(prepared.ResultsDirectory, "stream.jsonl")).Length);
        Assert.DoesNotContain(
            File.ReadAllLines(cli.EnvironmentNamesPath),
            name => name.StartsWith("CLAUDE", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("ANTHROPIC", StringComparison.OrdinalIgnoreCase));

        string appended = await File.ReadAllTextAsync(Path.Combine(prepared.ResultsDirectory, "system-prompt-append.md"), Token);
        Assert.StartsWith("# Ambiente di lavoro", appended);
        Assert.Contains("# Modalità di esecuzione: single-agent", appended);
        Assert.DoesNotContain("\r", appended);
        Assert.Equal(
            RunInputs.Sha256(File.ReadAllBytes(Path.Combine(prepared.ResultsDirectory, "system-prompt-append.md"))),
            record.Hashes.SystemPromptAppend);
        Assert.Equal(
            RunInputs.Sha256(File.ReadAllBytes(Path.Combine(prepared.ResultsDirectory, "cli-settings.json"))),
            record.Hashes.CliSettings);
        Assert.Equal(
            RunInputs.Sha256(File.ReadAllBytes(Path.Combine(prepared.ResultsDirectory, "mcp-config.json"))),
            record.Hashes.McpConfiguration);

        using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(recordPath, Token));
        Assert.Equal(2, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("completed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("pilot", json.RootElement.GetProperty("runKind").GetString());
    }

    [Fact]
    public async Task RunAsync_TokenBudgetReached_TerminatesSessionAndRecordsOverage()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"),
        [
            FakeClaudeCli.Init(),
            FakeClaudeCli.Assistant("msg_1", 10, 5000, 0, 3),
            "SLEEP 60000",
            FakeClaudeCli.Result(10, 5000, 0, 3),
        ]);

        var stopwatch = Stopwatch.StartNew();
        (SessionRecord record, _) = await runner.RunAsync(Request(prepared, cli) with { TokenBudget = 1000 }, Token);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"terminazione tardiva: {stopwatch.Elapsed}");
        Assert.Equal(SessionStatus.TokenLimitExceeded, record.Status);
        Assert.True(record.TerminatedByController);
        Assert.False(record.TokenMeasurementComplete);
        Assert.Equal(5013, record.TokensObserved);
        Assert.Equal(4013, record.TokenOverage);
    }

    [Fact]
    public async Task RunAsync_TimeLimitReached_TerminatesSessionPromptly()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"),
        [
            FakeClaudeCli.Init(),
            "SLEEP 60000",
            FakeClaudeCli.Result(1, 1, 1, 1),
        ]);

        var stopwatch = Stopwatch.StartNew();
        (SessionRecord record, _) = await runner.RunAsync(
            Request(prepared, cli) with { TimeLimit = TimeSpan.FromSeconds(4) }, Token);

        Assert.Equal(SessionStatus.TimeLimitExceeded, record.Status);
        Assert.True(record.TerminatedByController);
        Assert.InRange(record.AgentTaskDurationMs, 4000, 20000);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(40), $"terminazione tardiva: {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task RunAsync_UnexpectedToolInInit_RejectsConfigurationImmediately()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"),
        [
            FakeClaudeCli.Init("Bash"),
            "SLEEP 60000",
            FakeClaudeCli.Result(1, 1, 1, 1),
        ]);

        var stopwatch = Stopwatch.StartNew();
        (SessionRecord record, _) = await runner.RunAsync(Request(prepared, cli), Token);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"terminazione tardiva: {stopwatch.Elapsed}");
        Assert.Equal(SessionStatus.ConfigurationRejected, record.Status);
        Assert.Contains(record.ConfigurationViolations, violation => violation.Contains("Bash", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0, SessionStatus.UsageMeasurementError)]
    [InlineData(3, SessionStatus.CliFailed)]
    public async Task RunAsync_MissingResult_IsClassifiedByExitCode(int exitCode, SessionStatus expected)
    {
        PrepareRunOutput prepared = await PrepareAsync();
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"),
        [
            FakeClaudeCli.Init(),
            FakeClaudeCli.Assistant("msg_1", 1, 2, 3, 4),
            $"EXIT {exitCode}",
        ]);

        (SessionRecord record, _) = await runner.RunAsync(Request(prepared, cli), Token);

        Assert.Equal(expected, record.Status);
        Assert.Equal(exitCode, record.ExitCode);
        Assert.False(record.TokenMeasurementComplete);
    }

    [Fact]
    public async Task RunAsync_ErrorResult_IsCliFailed()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"),
        [
            FakeClaudeCli.Init(),
            FakeClaudeCli.Result(1, 1, 1, 1, subtype: "error_during_execution"),
        ]);

        (SessionRecord record, _) = await runner.RunAsync(Request(prepared, cli), Token);

        Assert.Equal(SessionStatus.CliFailed, record.Status);
        Assert.Equal("error_during_execution", record.Result!.Subtype);
    }

    /// <summary>Documenta lo stato e il flag di misura quando il risultato riuscito non contiene usage.</summary>
    [Fact]
    public async Task RunAsync_SuccessWithoutUsage_RecordsIncompleteMeasurement()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"),
        [
            FakeClaudeCli.Init(),
            FakeClaudeCli.Assistant("msg_1", 1, 2, 3, 4),
            """{"type":"result","subtype":"success","is_error":false}""",
        ]);

        (SessionRecord record, _) = await runner.RunAsync(Request(prepared, cli), Token);

        Assert.Equal(SessionStatus.Completed, record.Status);
        Assert.False(record.TokenMeasurementComplete);
        Assert.Null(record.AuthoritativeUsage);
        Assert.Equal(10, record.TokensObserved);
    }

    [Fact]
    public async Task RunAsync_AuthoritativeTotalAboveBudget_IsTokenLimitExceededEvenWithoutTermination()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        // Lo stream sottostima l'output; il result rivela il superamento.
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"),
        [
            FakeClaudeCli.Init(),
            FakeClaudeCli.Assistant("msg_1", 1, 500, 0, 1),
            FakeClaudeCli.Result(1, 500, 0, 900),
        ]);

        (SessionRecord record, _) = await runner.RunAsync(Request(prepared, cli) with { TokenBudget = 1000 }, Token);

        Assert.Equal(SessionStatus.TokenLimitExceeded, record.Status);
        Assert.False(record.TerminatedByController);
        Assert.Equal(1401, record.TokensObserved);
        Assert.Equal(401, record.TokenOverage);
        Assert.False(record.StreamMatchesAuthoritativeUsage);
    }

    [Fact]
    public async Task RunAsync_ProtectedMaterialAlteredDuringSession_IsRecorded()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"),
        [
            FakeClaudeCli.Init(),
            "TOUCH ../agent-root/public-reference/tests/public/t1/find.test.ts",
            "TOUCH ../agent-root/workspace/src/index.ts",
            FakeClaudeCli.Result(1, 1, 1, 1),
        ]);

        (SessionRecord record, _) = await runner.RunAsync(Request(prepared, cli), Token);

        Assert.Equal(["modificato: public-reference/tests/public/t1/find.test.ts"], record.ProtectedIntegrityDifferences);
    }

    [Fact]
    public async Task RunAsync_AreaAlteredBeforeStart_DoesNotStartSession()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        await File.WriteAllTextAsync(Path.Combine(prepared.AgentRoot, "workspace", "extra.ts"), "residuo", Token);
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"), [FakeClaudeCli.Init()]);

        SessionExecutionException exception = await Assert.ThrowsAsync<SessionExecutionException>(
            () => runner.RunAsync(Request(prepared, cli), Token));

        Assert.Contains("aggiunto: workspace/extra.ts", exception.Message);
        Assert.False(File.Exists(cli.PromptPath));
        Assert.False(File.Exists(Path.Combine(prepared.ResultsDirectory, "stream.jsonl")));
    }

    [Fact]
    public async Task RunAsync_SecondSessionOnSameArea_IsRejected()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"),
            [FakeClaudeCli.Init(), FakeClaudeCli.Result(1, 1, 1, 1)]);
        await runner.RunAsync(Request(prepared, cli), Token);

        await Assert.ThrowsAsync<SessionExecutionException>(() => runner.RunAsync(Request(prepared, cli), Token));
    }

    [Fact]
    public async Task RunAsync_WrongModel_IsRejectedBeforeCliStarts()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        var cli = new FakeClaudeCli(Path.Combine(temporaryBase, "cli"), [FakeClaudeCli.Result(1, 1, 1, 1)]);

        SessionExecutionException exception = await Assert.ThrowsAsync<SessionExecutionException>(
            () => runner.RunAsync(Request(prepared, cli) with { Model = "claude-opus-5-5" }, Token));

        Assert.Contains(ExperimentProtocol.RequiredModel, exception.Message);
        Assert.False(File.Exists(cli.PromptPath));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static System.Text.Encoding Encoding() => new System.Text.UTF8Encoding(false);

    private SingleAgentSessionRequest Request(PrepareRunOutput prepared, FakeClaudeCli cli) => new(
        Path.Combine(prepared.ResultsDirectory, PrepareRunCommand.PreparedRunFileName),
        cli.ExecutablePath,
        // Il server MCP non viene avviato dalla CLI finta: serve solo come file da identificare con hash.
        cli.ExecutablePath,
        new McpDevelopmentToolchain(cli.ExecutablePath, cli.ExecutablePath, cli.ExecutablePath),
        FakeClaudeCli.Model,
        TimeSpan.FromSeconds(60),
        1_000_000,
        new ExperimentRunMetadata(ExperimentRunKind.Pilot, 1, "test-seed", 1));

    private async Task<PrepareRunOutput> PrepareAsync()
    {
        repository.WriteFile("benchmark/BenchmarkTarget/src/index.ts", "export {};\n");
        repository.WriteFile("benchmark/BenchmarkTarget/tests/public/common.test.ts", "// comune\n");
        repository.WriteFile("benchmark/BenchmarkTarget/tests/public/t1/find.test.ts", "// t1\n");
        repository.WriteFile("docs/benchmark/task-specifications/T1-specifica-pubblica.md", Specification);
        repository.CommitAll("baseline");

        var command = new PrepareRunCommand(
            new RunAreaPreparer(
                new GitCommandRunner(),
                TimeProvider.System,
                TestDirectories.TempUserProfile()),
            TimeProvider.System);
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = await command.ExecuteAsync(
            [
                "--repository", repository.RootPath, "--commit", "main", "--task", "T1",
                "--runs-root", Path.Combine(temporaryBase, "runs"),
                "--results-root", Path.Combine(temporaryBase, "results"),
                "--run-id", "session-test",
            ],
            output,
            error,
            Token);
        Assert.True(exitCode == 0, error.ToString());
        return JsonSerializer.Deserialize<PrepareRunOutput>(output.ToString(), PrepareRunOutput.SerializerOptions)!;
    }
}
