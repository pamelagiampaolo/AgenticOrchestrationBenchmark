using AgenticExperimentRunner.SessionExecution;
using Xunit;

/// <summary>Verifica casi simulati di retry e uso mancante nello stream della CLI.</summary>
public sealed class RetryUsageTests
{
    /// <summary>Due messaggi osservati restano distinti e un evento ripetuto non viene sommato due volte.</summary>
    [Fact]
    public void Observe_SimulatedFailedAttemptAndRetry_CountsEachObservedMessageOnce()
    {
        var tracker = new StreamUsageTracker();

        tracker.Observe(FakeClaudeCli.Assistant("failed_attempt", 2, 10, 3, 4));
        tracker.Observe(FakeClaudeCli.Assistant("failed_attempt", 2, 10, 3, 4));
        tracker.Observe(FakeClaudeCli.Assistant("retry_attempt", 5, 20, 7, 8));
        tracker.Observe(FakeClaudeCli.Result(7, 30, 10, 12));

        Assert.Equal(2, tracker.Messages.Count);
        Assert.Equal(new TokenUsage(7, 30, 10, 12), tracker.ObservedUsage);
        Assert.Equal(tracker.ObservedUsage, tracker.Result!.Usage);
    }

    /// <summary>Un risultato senza campo usage non viene presentato come totale autorevole nullo.</summary>
    [Fact]
    public void Observe_ResultWithoutUsage_DoesNotInventAuthoritativeTotal()
    {
        var tracker = new StreamUsageTracker();

        tracker.Observe(FakeClaudeCli.Assistant("observed_attempt", 2, 10, 3, 4));
        tracker.Observe("""{"type":"result","subtype":"error_during_execution","is_error":true}""");

        Assert.NotNull(tracker.Result);
        Assert.Null(tracker.Result.Usage);
        Assert.Equal(new TokenUsage(2, 10, 3, 4), tracker.ObservedUsage);
    }

    /// <summary>Documenta il comportamento corrente quando un oggetto usage non contiene campi di token.</summary>
    [Fact]
    public void Observe_EmptyUsageObject_CurrentlyProducesZeroValues()
    {
        var tracker = new StreamUsageTracker();

        tracker.Observe("""{"type":"result","subtype":"success","is_error":false,"usage":{}}""");

        Assert.NotNull(tracker.Result);
        Assert.Equal(TokenUsage.Zero, tracker.Result.Usage);
    }
}
