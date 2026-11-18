using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class RunLimitControllerTests
{
    [Fact]
    public void ReportUsage_SumsLatestCumulativeUsagePerAgent()
    {
        using var limits = new RunLimitController(1000, TimeSpan.FromMinutes(5), TimeProvider.System);
        limits.Start();

        limits.ReportUsage("coordinator", new TokenUsage(0, 100, 0, 0));
        limits.ReportUsage("coordinator", new TokenUsage(0, 300, 0, 0));
        limits.ReportUsage("worker-01", new TokenUsage(10, 200, 5, 5));

        // Il valore riportato è cumulativo per agente: il secondo report sostituisce il primo.
        Assert.Equal(520, limits.ObservedTotalTokens);
        Assert.False(limits.Stopping.IsCancellationRequested);
        Assert.Null(limits.StopReason);
    }

    [Fact]
    public void ReportUsage_TotalReachingBudget_StopsRunSynchronously()
    {
        using var limits = new RunLimitController(1000, TimeSpan.FromMinutes(5), TimeProvider.System);
        limits.Start();

        limits.ReportUsage("coordinator", new TokenUsage(0, 600, 0, 0));
        limits.ReportUsage("worker-01", new TokenUsage(0, 400, 0, 0));

        Assert.True(limits.Stopping.IsCancellationRequested);
        Assert.True(limits.TokensExceeded);
        Assert.False(limits.TimeExceeded);
        Assert.Equal("budget di 1000 token raggiunto", limits.StopReason);
    }

    [Fact]
    public async Task TimeLimit_StopsRunAndKeepsFirstReason()
    {
        using var limits = new RunLimitController(1000, TimeSpan.FromMilliseconds(200), TimeProvider.System);
        limits.Start();

        await Task.Delay(Timeout.Infinite, limits.Stopping)
            .ContinueWith(_ => { }, TaskScheduler.Default)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        limits.Stop("motivo successivo");
        limits.ReportUsage("coordinator", new TokenUsage(0, 5000, 0, 0));

        Assert.True(limits.TimeExceeded);
        Assert.True(limits.TokensExceeded);
        Assert.StartsWith("limite di tempo", limits.StopReason);
    }

    [Fact]
    public void Stop_WithOtherReason_DoesNotMarkLimitsAsExceeded()
    {
        using var limits = new RunLimitController(1000, TimeSpan.FromMinutes(5), TimeProvider.System);
        limits.Start();

        limits.Stop("configurazione non conforme dell'agente worker-01");

        Assert.True(limits.Stopping.IsCancellationRequested);
        Assert.False(limits.TokensExceeded);
        Assert.False(limits.TimeExceeded);
        Assert.Equal("configurazione non conforme dell'agente worker-01", limits.StopReason);
    }

    [Fact]
    public void Start_Twice_Throws()
    {
        using var limits = new RunLimitController(1, TimeSpan.FromMinutes(1), TimeProvider.System);
        limits.Start();

        Assert.Throws<InvalidOperationException>(() => limits.Start());
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(-1, 1000)]
    [InlineData(1, 0)]
    public void Constructor_NonPositiveLimits_Throw(long budget, int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RunLimitController(budget, TimeSpan.FromMilliseconds(milliseconds), TimeProvider.System));
    }
}
