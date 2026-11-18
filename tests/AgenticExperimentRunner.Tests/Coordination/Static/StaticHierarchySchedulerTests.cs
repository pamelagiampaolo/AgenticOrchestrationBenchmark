using System.Text;
using AgenticExperimentRunner.Coordination;
using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class StaticHierarchySchedulerTests
{
    [Fact]
    public void Plan_HasExactlyTheApprovedRolesInFixedOrder()
    {
        Assert.Equal("hierarchical-static", StaticHierarchyPlan.ArchitectureName);
        Assert.Equal(["analyst", "implementer", "verifier"], StaticHierarchyPlan.Steps.Select(step => step.Role));
        Assert.Equal(3, StaticHierarchyPlan.Steps.Select(step => step.AgentId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(StaticHierarchyPlan.Steps, step => Assert.EndsWith($"static-{step.Role}.md", step.InstructionFileName));
    }

    [Fact]
    public async Task RunAsync_ExecutesEveryRoleOnceAndSequentially()
    {
        var observed = new List<string>();
        int active = 0;
        int maximumActive = 0;

        StaticHierarchyExecution execution = await StaticHierarchyScheduler.RunAsync(async (step, cancellationToken) =>
        {
            observed.Add(step.Role);
            active++;
            maximumActive = Math.Max(maximumActive, active);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            active--;
            return new StaticWorkerResult(true, $"{step.Role} completato", null);
        }, TestContext.Current.CancellationToken);

        Assert.True(execution.Completed);
        Assert.Equal(["analyst", "implementer", "verifier"], observed);
        Assert.Equal(1, maximumActive);
        Assert.Equal(3, execution.Outcomes.Count);
    }

    [Fact]
    public async Task RunAsync_StopsAtFirstFailedRole()
    {
        var observed = new List<string>();

        StaticHierarchyExecution execution = await StaticHierarchyScheduler.RunAsync((step, _) =>
        {
            observed.Add(step.Role);
            return Task.FromResult(step.Role == "implementer"
                ? new StaticWorkerResult(false, null, "implementazione fallita")
                : new StaticWorkerResult(true, "analisi completata", null));
        }, TestContext.Current.CancellationToken);

        Assert.False(execution.Completed);
        Assert.Equal(["analyst", "implementer"], observed);
        Assert.Equal("implementazione fallita", execution.Outcomes[^1].Result.FailureReason);
    }

    [Fact]
    public async Task RunAsync_TreatsMissingSuccessfulSummaryAsFailure()
    {
        StaticHierarchyExecution execution = await StaticHierarchyScheduler.RunAsync(
            (_, _) => Task.FromResult(new StaticWorkerResult(true, " ", null)),
            TestContext.Current.CancellationToken);

        StaticWorkerOutcome outcome = Assert.Single(execution.Outcomes);
        Assert.False(execution.Completed);
        Assert.False(outcome.Result.Succeeded);
        Assert.Contains("senza produrre un riepilogo", outcome.Result.FailureReason);
    }
}

public sealed class StaticDelegationHandlerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"static-delegation-{Guid.NewGuid():N}");

    [Fact]
    public async Task DelegateAsync_StartsThreeRoleSpecificProcessesUnderOneLimitController()
    {
        Directory.CreateDirectory(root);
        var clients = StaticHierarchyPlan.Steps.ToDictionary(
            step => step.Role,
            step => new FakeClaudeCli(
                Path.Combine(root, step.Role),
                [FakeClaudeCli.Init(), FakeHierarchyCli.Result($"{step.Role} completato", 1, 2, 3, 4)]),
            StringComparer.Ordinal);
        using var limits = new RunLimitController(1_000_000, TimeSpan.FromMinutes(1), TimeProvider.System);
        limits.Start();
        var handler = new StaticDelegationHandler(
            (agentId, role, task) => Launch(clients[role], agentId, role, task),
            limits,
            TimeProvider.System,
            TestContext.Current.CancellationToken);

        foreach ((StaticWorkerStep step, int index) in StaticHierarchyPlan.Steps.Select((step, index) => (step, index)))
        {
            DelegationOutcome outcome = await handler.DelegateAsync(
                new DelegationRequest(index + 1, $"compito {step.Role}"),
                TestContext.Current.CancellationToken);
            Assert.Equal(DelegationStatus.Completed, outcome.Status);
        }

        Assert.True(handler.IsComplete);
        Assert.Equal(["analyst", "implementer", "verifier"], handler.Workers.Select(worker => worker.Outcome.AgentId));
        Assert.Equal(["analyst", "implementer", "verifier"], handler.Workers.Select(worker => worker.Outcome.Role));
        Assert.Equal(3, handler.Events.Count);
        Assert.All(handler.Events, delegation => Assert.Equal(DelegationStatus.Completed, delegation.Status));
        Assert.Equal(30, handler.Workers.Sum(worker => worker.Outcome.Tracker.Result?.Usage?.TotalTokens ?? 0));
    }

    [Fact]
    public async Task DelegateAsync_RejectsFurtherWorkAfterFailedImplementer()
    {
        Directory.CreateDirectory(root);
        var analyst = new FakeClaudeCli(
            Path.Combine(root, "analyst"),
            [FakeClaudeCli.Init(), FakeHierarchyCli.Result("analisi", 1, 1, 1, 1)]);
        var implementer = new FakeClaudeCli(
            Path.Combine(root, "implementer"),
            [FakeClaudeCli.Init(), "EXIT 3"]);
        using var limits = new RunLimitController(1_000_000, TimeSpan.FromMinutes(1), TimeProvider.System);
        limits.Start();
        var handler = new StaticDelegationHandler(
            (agentId, role, task) => Launch(role == "analyst" ? analyst : implementer, agentId, role, task),
            limits,
            TimeProvider.System,
            TestContext.Current.CancellationToken);

        Assert.Equal(DelegationStatus.Completed, (await handler.DelegateAsync(
            new DelegationRequest(1, "analizza"), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(DelegationStatus.Failed, (await handler.DelegateAsync(
            new DelegationRequest(2, "implementa"), TestContext.Current.CancellationToken)).Status);
        DelegationOutcome rejected = await handler.DelegateAsync(
            new DelegationRequest(3, "verifica"), TestContext.Current.CancellationToken);

        Assert.Equal(DelegationStatus.Rejected, rejected.Status);
        Assert.False(handler.IsComplete);
        Assert.Equal(2, handler.Workers.Count);
        Assert.DoesNotContain(handler.Workers, worker => worker.Outcome.Role == "verifier");
    }

    private AgentLaunch Launch(FakeClaudeCli cli, string agentId, string role, string task)
    {
        string output = Path.Combine(root, $"output-{agentId}");
        Directory.CreateDirectory(output);
        return new AgentLaunch(
            agentId,
            role,
            cli.ExecutablePath,
            [],
            Encoding.UTF8.GetBytes(task),
            root,
            Path.Combine(output, "stream.jsonl"),
            Path.Combine(output, "stderr.txt"),
            initialization => ClaudeLaunchProfile.FindConfigurationViolations(initialization, FakeClaudeCli.Model));
    }

    public void Dispose() => TestDirectories.DeleteRecursively(root);
}
