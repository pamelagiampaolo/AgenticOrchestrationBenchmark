using AgenticExperimentRunner.Coordination;
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
