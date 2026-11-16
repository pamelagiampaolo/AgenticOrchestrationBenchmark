using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AgenticExperimentRunner.Coordination;
using Xunit;

/// <summary>
/// Integrazione relay–pipe–host nello stesso processo, con pipe reali.
/// </summary>
public sealed class CoordinationChannelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Delegation_RoundTripsThroughPipe_PreservingNonAsciiText()
    {
        var handler = new GatedDelegationHandler();
        await using RelayHarness harness = await RelayHarness.StartAsync(handler);

        harness.Input.Send(CoordinationMcpServerTests.ToolCall(1, """{"task":"Correggi la validità del prezzo €"}"""));
        PendingDelegation delegation = await handler.NextStartedAsync();
        delegation.Complete("Fatto: perché è così");
        JsonElement response = await harness.Output.NextResponseAsync();

        Assert.Equal("Correggi la validità del prezzo €", delegation.Request.Task);
        Assert.Equal(1, response.GetProperty("id").GetInt32());
        JsonElement result = response.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        Assert.Equal("Fatto: perché è così", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Ping_IsAnsweredWhileDelegationIsInProgress()
    {
        var handler = new GatedDelegationHandler();
        await using RelayHarness harness = await RelayHarness.StartAsync(handler);

        harness.Input.Send(CoordinationMcpServerTests.ToolCall(1, """{"task":"lungo"}"""));
        PendingDelegation delegation = await handler.NextStartedAsync();
        harness.Input.Send("""{"jsonrpc":"2.0","id":2,"method":"ping"}""");
        JsonElement ping = await harness.Output.NextResponseAsync();
        delegation.Complete("ok");
        JsonElement delegated = await harness.Output.NextResponseAsync();

        Assert.Equal(2, ping.GetProperty("id").GetInt32());
        Assert.Equal(1, delegated.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task ConcurrentDelegations_AreExecutedOneAtATimeInArrivalOrder()
    {
        var handler = new GatedDelegationHandler();
        await using RelayHarness harness = await RelayHarness.StartAsync(handler);

        harness.Input.Send(CoordinationMcpServerTests.ToolCall(1, """{"task":"primo"}"""));
        PendingDelegation first = await handler.NextStartedAsync();
        harness.Input.Send(CoordinationMcpServerTests.ToolCall(2, """{"task":"secondo"}"""));

        // Il secondo compito è già sulla pipe, ma non deve iniziare finché il primo è attivo.
        await Task.Delay(TimeSpan.FromMilliseconds(300), Cancellation);
        Assert.Equal(1, handler.StartedCount);
        first.Complete("uno");
        PendingDelegation second = await handler.NextStartedAsync();
        second.Complete("due");
        JsonElement firstResponse = await harness.Output.NextResponseAsync();
        JsonElement secondResponse = await harness.Output.NextResponseAsync();

        Assert.Equal("primo", first.Request.Task);
        Assert.Equal("secondo", second.Request.Task);
        Assert.Equal(1, handler.MaximumActive);
        Assert.Equal(1, firstResponse.GetProperty("id").GetInt32());
        Assert.Equal(2, secondResponse.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task InvalidToolCall_NeverReachesHost()
    {
        var handler = new GatedDelegationHandler();
        await using RelayHarness harness = await RelayHarness.StartAsync(handler);

        harness.Input.Send(CoordinationMcpServerTests.ToolCall(1, """{"task":"t","extra":1}"""));
        JsonElement response = await harness.Output.NextResponseAsync();

        Assert.Equal(-32602, response.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(0, handler.StartedCount);
    }

    [Fact]
    public async Task Host_AcceptsOnlyOneConnection()
    {
        await using RelayHarness harness = await RelayHarness.StartAsync(new GatedDelegationHandler());

        await Assert.ThrowsAsync<CoordinationChannelException>(() =>
            DelegationPipeClient.ConnectAsync(harness.Host.PipeName, TimeSpan.FromMilliseconds(300), Cancellation));
    }

    [Fact]
    public async Task Start_WithPipeNameAlreadyInUse_Fails()
    {
        await using CoordinationHost host = CoordinationHost.Start(CoordinationHost.CreatePipeName(), new GatedDelegationHandler());

        Assert.Throws<CoordinationChannelException>(() => CoordinationHost.Start(host.PipeName, new GatedDelegationHandler()));
    }

    [Fact]
    public async Task HandlerFailure_ClosesChannelAndPendingCallGetsChannelError()
    {
        await using RelayHarness harness = await RelayHarness.StartAsync(new FailingDelegationHandler());

        harness.Input.Send(CoordinationMcpServerTests.ToolCall(1, """{"task":"t"}"""));
        JsonElement response = await harness.Output.NextResponseAsync();
        harness.Input.Send(CoordinationMcpServerTests.ToolCall(2, """{"task":"t"}"""));
        JsonElement later = await harness.Output.NextResponseAsync();

        Assert.Equal(CoordinationMcpServer.ChannelUnavailableErrorCode, response.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(CoordinationMcpServer.ChannelUnavailableErrorCode, later.GetProperty("error").GetProperty("code").GetInt32());
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Host.Completion.WaitAsync(CoordinationTestTimeouts.Step, Cancellation));
    }

    [Fact]
    public async Task HostDisposedDuringDelegation_PendingCallGetsChannelError()
    {
        var handler = new GatedDelegationHandler();
        await using RelayHarness harness = await RelayHarness.StartAsync(handler);

        harness.Input.Send(CoordinationMcpServerTests.ToolCall(1, """{"task":"t"}"""));
        await handler.NextStartedAsync();
        await harness.Host.DisposeAsync();
        JsonElement response = await harness.Output.NextResponseAsync();

        Assert.Equal(1, response.GetProperty("id").GetInt32());
        Assert.Equal(CoordinationMcpServer.ChannelUnavailableErrorCode, response.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task RelayInputClosed_RelayStopsAndHostEndsNormallyWhenResponseHasNoReceiver()
    {
        var handler = new GatedDelegationHandler();
        await using RelayHarness harness = await RelayHarness.StartAsync(handler);

        harness.Input.Send(CoordinationMcpServerTests.ToolCall(1, """{"task":"t"}"""));
        PendingDelegation delegation = await handler.NextStartedAsync();
        harness.Input.EndInput();
        await harness.Relay.WaitAsync(CoordinationTestTimeouts.Step, Cancellation);
        await harness.Client.DisposeAsync();
        delegation.Complete("tardivo");

        // La risposta tardiva non ha destinatario: il relay non scrive nulla dopo la chiusura.
        await harness.Host.Completion.WaitAsync(CoordinationTestTimeouts.Step, Cancellation);
        Assert.False(harness.Output.TryReadResponse(out _));
        Assert.Equal(1, handler.StartedCount);
    }

    [Theory]
    [InlineData("{non json")]
    [InlineData("""{"requestId":1,"task":"t","extra":true}""")]
    [InlineData("""{"requestId":1,"task":""}""")]
    [InlineData("""{"requestId":0,"task":"t"}""")]
    [InlineData("""{"task":"t"}""")]
    public async Task Host_InvalidWireMessage_FailsWithoutCallingHandler(string line)
    {
        var handler = new GatedDelegationHandler();
        await using CoordinationHost host = CoordinationHost.Start(CoordinationHost.CreatePipeName(), handler);
        await using var raw = new NamedPipeClientStream(".", host.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await raw.ConnectAsync(CoordinationTestTimeouts.Step, Cancellation);

        byte[] bytes = new UTF8Encoding(false).GetBytes(line + "\n");
        await raw.WriteAsync(bytes, Cancellation);
        await raw.FlushAsync(Cancellation);

        await Assert.ThrowsAsync<CoordinationChannelException>(() => host.Completion.WaitAsync(CoordinationTestTimeouts.Step, Cancellation));
        Assert.Equal(0, handler.StartedCount);
    }

    /// <summary>Host, client della pipe e relay collegati come in una sessione reale, ma nello stesso processo.</summary>
    private sealed class RelayHarness : IAsyncDisposable
    {
        private RelayHarness(CoordinationHost host, DelegationPipeClient client, ScriptedInput input, CollectingOutput output, Task relay)
        {
            Host = host;
            Client = client;
            Input = input;
            Output = output;
            Relay = relay;
        }

        public CoordinationHost Host { get; }

        public DelegationPipeClient Client { get; }

        public ScriptedInput Input { get; }

        public CollectingOutput Output { get; }

        public Task Relay { get; }

        public static async Task<RelayHarness> StartAsync(IDelegationHandler handler)
        {
            CoordinationHost host = CoordinationHost.Start(CoordinationHost.CreatePipeName(), handler);
            DelegationPipeClient client = await DelegationPipeClient.ConnectAsync(host.PipeName, CoordinationTestTimeouts.Step, Cancellation);
            var input = new ScriptedInput();
            var output = new CollectingOutput();
            Task relay = CoordinationRelay.RunAsync(input, output, new CoordinationMcpServer(client.DelegateAsync), Cancellation);
            return new RelayHarness(host, client, input, output, relay);
        }

        public async ValueTask DisposeAsync()
        {
            Input.EndInput();
            await Relay.WaitAsync(CoordinationTestTimeouts.Step);
            await Client.DisposeAsync();
            await Host.DisposeAsync();
        }
    }
}
