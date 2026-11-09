using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgenticExperimentRunner.Coordination;
using Xunit;

/// <summary>
/// Prove sul relay avviato come processo reale, come lo avvia la CLI del coordinatore.
/// </summary>
public sealed class CoordinationServerCommandTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static string RunnerExecutable => Path.Combine(AppContext.BaseDirectory, "AgenticExperimentRunner.exe");

    [Fact]
    public async Task RelayProcess_ServesMcpSessionAndDelegatesThroughHost()
    {
        var handler = new GatedDelegationHandler();
        await using CoordinationHost host = CoordinationHost.Start(CoordinationHost.CreatePipeName(), handler);
        using Process relay = StartRelay("--pipe", host.PipeName);

        await SendAsync(relay, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""");
        JsonElement initialize = await ReadResponseAsync(relay);
        await SendAsync(relay, """{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        await SendAsync(relay, """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");
        JsonElement tools = await ReadResponseAsync(relay);
        await SendAsync(relay, CoordinationMcpServerTests.ToolCall(3, """{"task":"Aggiungi la validazione: quantità ≥ 1"}"""));
        PendingDelegation delegation = await handler.NextStartedAsync();
        delegation.Complete("Validazione aggiunta, test non eseguiti.");
        JsonElement call = await ReadResponseAsync(relay);
        relay.StandardInput.Close();
        await relay.WaitForExitAsync(Cancellation).WaitAsync(CoordinationTestTimeouts.Step, Cancellation);

        Assert.Equal("benchmark-coordination", initialize.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.Equal("delegate_task", tools.GetProperty("result").GetProperty("tools")[0].GetProperty("name").GetString());
        Assert.Equal("Aggiungi la validazione: quantità ≥ 1", delegation.Request.Task);
        Assert.Equal(3, call.GetProperty("id").GetInt32());
        Assert.Equal("Validazione aggiunta, test non eseguiti.", call.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal(CoordinationServerCommand.SuccessExitCode, relay.ExitCode);
        await host.Completion.WaitAsync(CoordinationTestTimeouts.Step, Cancellation);
    }

    [Fact]
    public async Task RelayProcess_WithoutHost_ExitsWithChannelFailureAndDiagnosticOnStandardError()
    {
        using Process relay = StartRelay("--pipe", CoordinationHost.CreatePipeName(), "--connect-timeout-ms", "200");

        Task<string> standardError = relay.StandardError.ReadToEndAsync(Cancellation);
        await relay.WaitForExitAsync(Cancellation).WaitAsync(CoordinationTestTimeouts.Step, Cancellation);

        Assert.Equal(CoordinationServerCommand.ChannelFailedExitCode, relay.ExitCode);
        Assert.Contains("Canale di coordinamento non disponibile", await standardError);
        Assert.Equal(string.Empty, await relay.StandardOutput.ReadToEndAsync(Cancellation));
    }

    [Theory]
    [InlineData]
    [InlineData("--pipe", "")]
    [InlineData("--pipe", "../altro")]
    [InlineData("--pipe", @"\\.\pipe\x")]
    [InlineData("--pipe", "p", "--connect-timeout-ms", "0")]
    [InlineData("--pipe", "p", "--connect-timeout-ms", "-5")]
    [InlineData("--pipe", "p", "--sconosciuta", "1")]
    public void TryParse_InvalidArguments_AreRejected(params string[] arguments)
    {
        Assert.False(CoordinationServerCommand.TryParse(arguments, out _, out _, out string? error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryParse_ValidArguments_ReturnPipeAndTimeout()
    {
        Assert.True(CoordinationServerCommand.TryParse(
            ["--pipe", "agentic-coordination-abc", "--connect-timeout-ms", "1500"], out string? pipe, out TimeSpan timeout, out _));
        Assert.Equal("agentic-coordination-abc", pipe);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), timeout);
    }

    [Fact]
    public async Task RelayProcess_InvalidArguments_ExitWithUsageError()
    {
        using Process relay = StartRelay("--pipe", "../x");

        await relay.WaitForExitAsync(Cancellation).WaitAsync(CoordinationTestTimeouts.Step, Cancellation);

        Assert.Equal(CoordinationServerCommand.UsageErrorExitCode, relay.ExitCode);
    }

    private static Process StartRelay(params string[] options)
    {
        var startInfo = new ProcessStartInfo(RunnerExecutable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardInputEncoding = Utf8WithoutBom,
            StandardOutputEncoding = Utf8WithoutBom,
            StandardErrorEncoding = Utf8WithoutBom,
        };
        startInfo.ArgumentList.Add(CoordinationServerCommand.Name);
        foreach (string option in options)
        {
            startInfo.ArgumentList.Add(option);
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Relay non avviato.");
    }

    private static async Task SendAsync(Process relay, string line)
    {
        await relay.StandardInput.WriteAsync(line + "\n");
        await relay.StandardInput.FlushAsync(Cancellation);
    }

    private static async Task<JsonElement> ReadResponseAsync(Process relay)
    {
        string? line = await relay.StandardOutput.ReadLineAsync(Cancellation).AsTask().WaitAsync(CoordinationTestTimeouts.Step, Cancellation);
        Assert.NotNull(line);
        using JsonDocument document = JsonDocument.Parse(line);
        return document.RootElement.Clone();
    }
}
