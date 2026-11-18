using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using AgenticExperimentRunner.Coordination;
using Xunit;

/// <summary>
/// Gestore delle deleghe controllato dal test: ogni delega resta in attesa finché il test
/// non la rilascia, e viene registrato il massimo di deleghe contemporanee osservato.
/// </summary>
internal sealed class GatedDelegationHandler : IDelegationHandler
{
    private readonly Channel<PendingDelegation> started = Channel.CreateUnbounded<PendingDelegation>();
    private int active;
    private int maximumActive;

    public int MaximumActive => Volatile.Read(ref maximumActive);

    public int StartedCount { get; private set; }

    public async Task<DelegationOutcome> DelegateAsync(DelegationRequest request, CancellationToken cancellationToken)
    {
        int nowActive = Interlocked.Increment(ref active);
        InterlockedMax(ref maximumActive, nowActive);
        StartedCount++;
        try
        {
            var pending = new PendingDelegation(request);
            started.Writer.TryWrite(pending);
            return await pending.Release.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref active);
        }
    }

    /// <summary>Attende l'avvio della prossima delega.</summary>
    public Task<PendingDelegation> NextStartedAsync() =>
        started.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(CoordinationTestTimeouts.Step);

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value
            && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}

/// <summary>Delega in corso nel <see cref="GatedDelegationHandler"/>.</summary>
internal sealed record PendingDelegation(DelegationRequest Request)
{
    public TaskCompletionSource<DelegationOutcome> Release { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Complete(string text) => Release.TrySetResult(new DelegationOutcome(DelegationStatus.Completed, text));
}

/// <summary>Gestore che fallisce: simula un guasto dell'orchestratore.</summary>
internal sealed class FailingDelegationHandler : IDelegationHandler
{
    public Task<DelegationOutcome> DelegateAsync(DelegationRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("guasto simulato dell'orchestratore");
}

/// <summary>Input del relay alimentato riga per riga dal test.</summary>
internal sealed class ScriptedInput : TextReader
{
    private readonly Channel<string> lines = Channel.CreateUnbounded<string>();

    public void Send(string line) => lines.Writer.TryWrite(line);

    public void EndInput() => lines.Writer.TryComplete();

    public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await lines.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }
}

/// <summary>Output del relay letto riga per riga dal test.</summary>
internal sealed class CollectingOutput : TextWriter
{
    private readonly Channel<string> lines = Channel.CreateUnbounded<string>();

    public override Encoding Encoding => Encoding.UTF8;

    public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        lines.Writer.TryWrite(buffer.ToString());
        return Task.CompletedTask;
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<JsonElement> NextResponseAsync()
    {
        string line = await lines.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(CoordinationTestTimeouts.Step);
        using JsonDocument document = JsonDocument.Parse(line);
        return document.RootElement.Clone();
    }

    public bool TryReadResponse(out string? line) => lines.Reader.TryRead(out line);
}

internal static class CoordinationTestTimeouts
{
    /// <summary>Attesa massima di un singolo passo asincrono nei test.</summary>
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(10);
}
