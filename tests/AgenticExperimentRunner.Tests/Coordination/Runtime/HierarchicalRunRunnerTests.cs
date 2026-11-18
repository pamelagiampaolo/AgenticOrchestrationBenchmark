using System.Diagnostics;
using System.Text.Json;
using AgenticExperimentRunner.Coordination;
using AgenticExperimentRunner.RunPreparation;
using AgenticExperimentRunner.SessionExecution;
using Xunit;

/// <summary>
/// Test di integrazione della run gerarchica: CLI finta, relay e pipe reali, nessuna chiamata LLM.
/// </summary>
public sealed class HierarchicalRunRunnerTests : IDisposable
{
    // La prima riga sceglie il copione del coordinatore finto.
    private const string Specification = "SCRIPT coordinator\n# T1\n\nSpecifica con accenti: perché è così.\n";

    private readonly TemporaryGitRepository repository = new();
    private readonly string temporaryBase = Path.Combine(Path.GetTempPath(), $"a5-hierarchy-{Guid.NewGuid():N}");
    private readonly HierarchicalRunRunner runner = new(
        TimeProvider.System, Path.Combine(AppContext.BaseDirectory, "Instructions"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string RelayExecutable => Path.Combine(AppContext.BaseDirectory, "AgenticExperimentRunner.exe");

    public void Dispose()
    {
        repository.Dispose();
        TestDirectories.DeleteRecursively(temporaryBase);
    }

    [Fact]
    public async Task RunAsync_OneDelegation_CompletesAndRecordsAgentsDelegationAndTotals()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        FakeHierarchyCli cli = CreateCli(
            coordinator:
            [
                FakeHierarchyCli.CoordinatorInit(),
                FakeClaudeCli.Assistant("c1", 2, 100, 10, 5),
                "DELEGATE SCRIPT worker-ok",
                FakeClaudeCli.Assistant("c2", 3, 120, 0, 7),
                FakeHierarchyCli.Result("riepilogo del coordinatore", 5, 220, 10, 12),
            ],
            workers: new() { ["worker-ok"] = [FakeClaudeCli.Init(), FakeClaudeCli.Assistant("w1", 1, 50, 5, 4), FakeHierarchyCli.Result("lavoro svolto", 1, 50, 5, 4)] });

        (HierarchicalRunRecord record, string recordPath) = await runner.RunAsync(Request(prepared, cli), Token);

        Assert.Equal(SessionStatus.Completed, record.Status);
        Assert.Equal(Path.Combine(prepared.ResultsDirectory, "run-record.json"), recordPath);
        Assert.Equal("hierarchical-adaptive", record.Architecture);
        Assert.Equal(ExperimentRunKind.Pilot, record.RunKind);
        Assert.Equal(1, record.Repetition);
        Assert.Equal("test-seed", record.OrderSeed);
        Assert.Equal(1, record.OrderPosition);
        Assert.Equal(["coordinator", "worker-01"], record.Agents.Select(agent => agent.AgentId));
        Assert.Equal(["coordinator", "worker"], record.Agents.Select(agent => agent.Role));
        Assert.All(record.Agents, agent => Assert.Empty(agent.ConfigurationViolations));
        Assert.Equal(new TokenUsage(6, 270, 15, 16), record.TotalUsage);
        Assert.Equal(record.TotalUsage.TotalTokens, record.TokensObserved);
        Assert.True(record.TokenMeasurementComplete);
        Assert.Equal(new TokenUsage(1, 50, 5, 4), record.UsageByRole["worker"]);
        Assert.Equal(new DelegationCounts(1, 1, 1, 0, 0), record.DelegationCounts);
        Assert.Equal(1, record.MaxConcurrentWorkers);
        DelegationEvent delegation = Assert.Single(record.Delegations);
        Assert.Equal(DelegationStatus.Completed, delegation.Status);
        Assert.Equal("worker-01", delegation.AgentId);
        Assert.Equal(delegation.RequestId, record.Agents[1].DelegationRequestId);
        Assert.Empty(record.ProtectedIntegrityDifferences);

        // Il coordinatore riceve il riepilogo finale del lavoratore come esito della delega.
        JsonElement response = Assert.Single(cli.DelegationResponses());
        Assert.False(response.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("lavoro svolto", response.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());

        // Prompt: specifica al coordinatore, solo il compito al lavoratore (anche nel prompt.md registrato).
        Assert.Equal(Specification, Assert.Single(cli.PromptsOf("coordinator")));
        Assert.Equal("SCRIPT worker-ok", Assert.Single(cli.PromptsOf("worker-ok")));
        Assert.Equal("SCRIPT worker-ok", File.ReadAllText(Path.Combine(prepared.ResultsDirectory, "agents", "worker-01", "prompt.md")));
        Assert.DoesNotContain(
            cli.AllEnvironmentNames(),
            name => name.StartsWith("CLAUDE", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("ANTHROPIC", StringComparison.OrdinalIgnoreCase));

        // System prompt dei due ruoli e configurazione del coordinatore con due server.
        string coordinatorPrompt = File.ReadAllText(Path.Combine(prepared.ResultsDirectory, "coordinator-system-prompt-append.md"));
        string workerPrompt = File.ReadAllText(Path.Combine(prepared.ResultsDirectory, "worker-system-prompt-append.md"));
        Assert.StartsWith("# Ambiente di lavoro", coordinatorPrompt);
        Assert.Contains("coordinatore", coordinatorPrompt);
        Assert.Contains("lavoratore incaricato", workerPrompt);
        using JsonDocument coordinatorConfig = JsonDocument.Parse(File.ReadAllText(Path.Combine(prepared.ResultsDirectory, "coordinator-mcp-config.json")));
        JsonElement coordinationServer = coordinatorConfig.RootElement.GetProperty("mcpServers").GetProperty("benchmark_coordination");
        Assert.Equal(RelayExecutable, coordinationServer.GetProperty("command").GetString());
        Assert.Equal(60_000, coordinationServer.GetProperty("timeout").GetInt64());
        using JsonDocument workerConfig = JsonDocument.Parse(File.ReadAllText(Path.Combine(prepared.ResultsDirectory, "worker-mcp-config.json")));
        Assert.Equal(["benchmark_workspace"], workerConfig.RootElement.GetProperty("mcpServers").EnumerateObject().Select(server => server.Name));
        Assert.True(File.Exists(Path.Combine(prepared.ResultsDirectory, "agents", "coordinator", "stream.jsonl")));
        Assert.True(File.Exists(Path.Combine(prepared.ResultsDirectory, "agents", "worker-01", "stream.jsonl")));
        Assert.Equal(RunInputs.Sha256(CoordinationMcpServer.DescribeTools()), record.Hashes.DelegateToolDefinition);
        Assert.NotNull(record.Hashes.CoordinationServerAssembly);
        Assert.Equal("9.9.9 fake", record.Toolchain.NodeVersion);
        Assert.Equal(cli.ExecutablePath, record.Toolchain.VitestPath);

        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(recordPath));
        Assert.Equal(2, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("completed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("pilot", json.RootElement.GetProperty("runKind").GetString());
        Assert.Equal("completed", json.RootElement.GetProperty("delegations")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task RunAsync_DelegationsBeyondMaximum_AreRejectedWithoutStartingWorkers()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        FakeHierarchyCli cli = CreateCli(
            coordinator:
            [
                FakeHierarchyCli.CoordinatorInit(),
                "DELEGATE SCRIPT worker-ok",
                "DELEGATE SCRIPT worker-ok",
                FakeHierarchyCli.Result("fine", 1, 1, 1, 1),
            ],
            workers: new() { ["worker-ok"] = [FakeClaudeCli.Init(), FakeHierarchyCli.Result("ok", 1, 1, 1, 1)] });

        (HierarchicalRunRecord record, _) = await runner.RunAsync(Request(prepared, cli) with { MaxDelegations = 1 }, Token);

        Assert.Equal(SessionStatus.Completed, record.Status);
        Assert.Equal(new DelegationCounts(2, 1, 1, 0, 1), record.DelegationCounts);
        Assert.Single(cli.PromptsOf("worker-ok"));
        DelegationEvent rejected = record.Delegations[1];
        Assert.Equal(DelegationStatus.Rejected, rejected.Status);
        Assert.Null(rejected.AgentId);
        Assert.Contains("massimo di 1 deleghe", rejected.Reason);
        JsonElement response = cli.DelegationResponses()[1];
        Assert.True(response.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task RunAsync_WorkerWithoutResult_DelegationFailsAndMeasurementIsIncomplete()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        FakeHierarchyCli cli = CreateCli(
            coordinator:
            [
                FakeHierarchyCli.CoordinatorInit(),
                "DELEGATE SCRIPT worker-crash",
                FakeHierarchyCli.Result("fine", 1, 1, 1, 1),
            ],
            workers: new() { ["worker-crash"] = [FakeClaudeCli.Init(), FakeClaudeCli.Assistant("w1", 1, 10, 0, 2), "EXIT 3"] });

        (HierarchicalRunRecord record, _) = await runner.RunAsync(Request(prepared, cli), Token);

        Assert.Equal(SessionStatus.UsageMeasurementError, record.Status);
        Assert.False(record.TokenMeasurementComplete);
        Assert.Equal(new DelegationCounts(1, 1, 0, 1, 0), record.DelegationCounts);
        Assert.Contains("codice 3", record.Delegations[0].Reason);
        Assert.True(cli.DelegationResponses()[0].GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task RunAsync_BudgetReachedBySumOfAgents_StopsWorkerAndCoordinatorPromptly()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        FakeHierarchyCli cli = CreateCli(
            coordinator:
            [
                FakeHierarchyCli.CoordinatorInit(),
                FakeClaudeCli.Assistant("c1", 0, 600, 0, 0),
                "DELEGATE SCRIPT worker-slow",
                FakeHierarchyCli.Result("fine", 1, 1, 1, 1),
            ],
            workers: new() { ["worker-slow"] = [FakeClaudeCli.Init(), FakeClaudeCli.Assistant("w1", 0, 600, 0, 0), "SLEEP 60000", FakeHierarchyCli.Result("ok", 1, 1, 1, 1)] });

        var stopwatch = Stopwatch.StartNew();
        (HierarchicalRunRecord record, _) = await runner.RunAsync(Request(prepared, cli) with { TokenBudget = 1000 }, Token);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(40), $"terminazione tardiva: {stopwatch.Elapsed}");
        Assert.Equal(SessionStatus.TokenLimitExceeded, record.Status);
        Assert.Equal(1200, record.TokensObserved);
        Assert.Equal(200, record.TokenOverage);
        Assert.All(record.Agents, agent => Assert.True(agent.TerminatedByController));
        Assert.Contains("budget di 1000 token", record.StopReason);
        Assert.Equal(DelegationStatus.Failed, Assert.Single(record.Delegations).Status);
    }

    [Fact]
    public async Task RunAsync_TimeLimitDuringDelegation_StopsAllAgents()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        FakeHierarchyCli cli = CreateCli(
            coordinator:
            [
                FakeHierarchyCli.CoordinatorInit(),
                "DELEGATE SCRIPT worker-slow",
                FakeHierarchyCli.Result("fine", 1, 1, 1, 1),
            ],
            workers: new() { ["worker-slow"] = [FakeClaudeCli.Init(), "SLEEP 60000", FakeHierarchyCli.Result("ok", 1, 1, 1, 1)] });

        var stopwatch = Stopwatch.StartNew();
        (HierarchicalRunRecord record, _) = await runner.RunAsync(
            Request(prepared, cli) with { TimeLimit = TimeSpan.FromSeconds(8) }, Token);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(40), $"terminazione tardiva: {stopwatch.Elapsed}");
        Assert.Equal(SessionStatus.TimeLimitExceeded, record.Status);
        Assert.All(record.Agents, agent => Assert.True(agent.TerminatedByController));
        Assert.Equal(2, record.Agents.Count);
    }

    [Fact]
    public async Task RunAsync_WorkerWithUnexpectedConfiguration_RejectsRunAndStopsCoordinator()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        FakeHierarchyCli cli = CreateCli(
            coordinator:
            [
                FakeHierarchyCli.CoordinatorInit(),
                "DELEGATE SCRIPT worker-bash",
                "SLEEP 60000",
                FakeHierarchyCli.Result("fine", 1, 1, 1, 1),
            ],
            workers: new() { ["worker-bash"] = [FakeClaudeCli.Init("Bash"), "SLEEP 60000"] });

        var stopwatch = Stopwatch.StartNew();
        (HierarchicalRunRecord record, _) = await runner.RunAsync(Request(prepared, cli), Token);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(40), $"terminazione tardiva: {stopwatch.Elapsed}");
        Assert.Equal(SessionStatus.ConfigurationRejected, record.Status);
        Assert.Contains("Bash", Assert.Single(record.Agents[1].ConfigurationViolations));
        Assert.Contains("worker-01", record.StopReason);
        Assert.True(record.Agents[0].TerminatedByController);
    }

    [Fact]
    public async Task RunAsync_CoordinatorWithoutDelegateTool_IsRejected()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        FakeHierarchyCli cli = CreateCli(
            coordinator: [FakeClaudeCli.Init(), "SLEEP 60000"],
            workers: []);

        (HierarchicalRunRecord record, _) = await runner.RunAsync(Request(prepared, cli), Token);

        Assert.Equal(SessionStatus.ConfigurationRejected, record.Status);
        Assert.NotEmpty(record.Agents[0].ConfigurationViolations);
    }

    [Fact]
    public async Task RunAsync_ResultsDirectoryAlreadyUsed_IsRefusedWithoutStarting()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        File.WriteAllText(Path.Combine(prepared.ResultsDirectory, "session-record.json"), "{}");
        FakeHierarchyCli cli = CreateCli(coordinator: [FakeHierarchyCli.CoordinatorInit()], workers: []);

        SessionExecutionException exception = await Assert.ThrowsAsync<SessionExecutionException>(
            () => runner.RunAsync(Request(prepared, cli), Token));

        Assert.Contains("session-record.json", exception.Message);
        Assert.Empty(cli.PromptsOf("coordinator"));
    }

    [Fact]
    public async Task StaticRun_ExecutesThreeFixedRolesAndWritesStaticRecord()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        FakeHierarchyCli cli = CreateCli(
            coordinator:
            [
                FakeHierarchyCli.CoordinatorInit(),
                "DELEGATE SCRIPT analyst",
                "DELEGATE SCRIPT implementer",
                "DELEGATE SCRIPT verifier",
                FakeHierarchyCli.Result("coordinamento completato", 1, 1, 1, 1),
            ],
            workers: new()
            {
                ["analyst"] = [FakeClaudeCli.Init(), FakeHierarchyCli.Result("piano", 1, 1, 1, 1)],
                ["implementer"] = [FakeClaudeCli.Init(), FakeHierarchyCli.Result("implementato", 1, 1, 1, 1)],
                ["verifier"] = [FakeClaudeCli.Init(), FakeHierarchyCli.Result("verificato", 1, 1, 1, 1)],
            });
        HierarchicalRunRunner staticRunner = HierarchicalRunRunner.CreateStatic(
            TimeProvider.System,
            Path.Combine(AppContext.BaseDirectory, "Instructions"));

        (HierarchicalRunRecord record, _) = await staticRunner.RunAsync(Request(prepared, cli), Token);

        Assert.Equal(SessionStatus.Completed, record.Status);
        Assert.Equal("hierarchical-static", record.Architecture);
        Assert.Equal(3, record.MaxDelegations);
        Assert.Equal(["coordinator", "analyst", "implementer", "verifier"], record.Agents.Select(agent => agent.AgentId));
        Assert.Equal(["coordinator", "analyst", "implementer", "verifier"], record.Agents.Select(agent => agent.Role));
        Assert.Equal(new DelegationCounts(3, 3, 3, 0, 0), record.DelegationCounts);
        Assert.Equal(1, record.MaxConcurrentWorkers);
        foreach (StaticWorkerStep step in StaticHierarchyPlan.Steps)
        {
            string prompt = File.ReadAllText(Path.Combine(
                prepared.ResultsDirectory,
                "agents",
                step.AgentId,
                "system-prompt-append.md"));
            Assert.Contains($"Ruolo statico: {step.Role}", prompt);
        }
        Assert.Contains(
            "esattamente tre deleghe",
            File.ReadAllText(Path.Combine(prepared.ResultsDirectory, "coordinator-system-prompt-append.md")));
    }

    [Fact]
    public async Task StaticRun_CoordinatorEndingBeforeAllRoles_IsCliFailed()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        FakeHierarchyCli cli = CreateCli(
            coordinator:
            [
                FakeHierarchyCli.CoordinatorInit(),
                "DELEGATE SCRIPT analyst",
                FakeHierarchyCli.Result("fine anticipata", 1, 1, 1, 1),
            ],
            workers: new()
            {
                ["analyst"] = [FakeClaudeCli.Init(), FakeHierarchyCli.Result("piano", 1, 1, 1, 1)],
            });
        HierarchicalRunRunner staticRunner = HierarchicalRunRunner.CreateStatic(
            TimeProvider.System,
            Path.Combine(AppContext.BaseDirectory, "Instructions"));

        (HierarchicalRunRecord record, _) = await staticRunner.RunAsync(Request(prepared, cli), Token);

        Assert.Equal(SessionStatus.CliFailed, record.Status);
        Assert.Equal("il coordinatore non ha completato i tre passaggi statici", record.StopReason);
        Assert.Equal(["coordinator", "analyst"], record.Agents.Select(agent => agent.Role));
    }

    [Fact]
    public void MaxConcurrent_CountsOverlapsAndTreatsTouchingIntervalsAsSequential()
    {
        DateTimeOffset origin = DateTimeOffset.UnixEpoch;

        Assert.Equal(0, HierarchicalRunRunner.MaxConcurrent([]));
        Assert.Equal(1, HierarchicalRunRunner.MaxConcurrent(
            [(origin, origin.AddSeconds(5)), (origin.AddSeconds(5), origin.AddSeconds(9))]));
        Assert.Equal(2, HierarchicalRunRunner.MaxConcurrent(
            [(origin, origin.AddSeconds(5)), (origin.AddSeconds(4), origin.AddSeconds(9))]));
    }

    private FakeHierarchyCli CreateCli(IEnumerable<string> coordinator, Dictionary<string, IEnumerable<string>> workers)
    {
        var scripts = new Dictionary<string, IEnumerable<string>>(workers) { ["coordinator"] = coordinator };
        return new FakeHierarchyCli(Path.Combine(temporaryBase, "cli"), scripts);
    }

    [Fact]
    public async Task RunAsync_WrongModel_IsRejectedBeforeCoordinatorStarts()
    {
        PrepareRunOutput prepared = await PrepareAsync();
        FakeHierarchyCli cli = CreateCli([FakeHierarchyCli.Result("fine", 1, 1, 1, 1)], []);

        SessionExecutionException exception = await Assert.ThrowsAsync<SessionExecutionException>(
            () => runner.RunAsync(Request(prepared, cli) with { Model = "claude-opus-5-5" }, Token));

        Assert.Contains(ExperimentProtocol.RequiredModel, exception.Message);
        Assert.Empty(cli.PromptsOf("coordinator"));
    }

    private static HierarchicalRunRequest Request(PrepareRunOutput prepared, FakeHierarchyCli cli) => new(
        Path.Combine(prepared.ResultsDirectory, PrepareRunCommand.PreparedRunFileName),
        cli.ExecutablePath,
        // Il server del workspace non viene avviato dalla CLI finta: serve solo come file da identificare con hash.
        cli.ExecutablePath,
        new McpDevelopmentToolchain(cli.ExecutablePath, cli.ExecutablePath, cli.ExecutablePath),
        RelayExecutable,
        FakeClaudeCli.Model,
        TimeSpan.FromSeconds(60),
        1_000_000,
        10,
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
                "--run-id", "hierarchy-test",
            ],
            output,
            error,
            Token);
        Assert.True(exitCode == 0, error.ToString());
        return JsonSerializer.Deserialize<PrepareRunOutput>(output.ToString(), PrepareRunOutput.SerializerOptions)!;
    }
}
