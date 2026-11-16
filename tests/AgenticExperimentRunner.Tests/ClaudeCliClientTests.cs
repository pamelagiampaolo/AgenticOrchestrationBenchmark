using Xunit;

public sealed class ClaudeCliClientTests
{
    [Fact]
    public void ParseOutput_WithCompleteValidResult_ExtractsAllValues()
    {
        string output = string.Join(
            Environment.NewLine,
            "{\"type\":\"system\",\"session_id\":\"session-1\",\"model\":\"model-a\"}",
            "{\"type\":\"assistant\",\"message\":{\"model\":\"model-a\"}}",
            "{\"type\":\"result\",\"result\":\"Risposta\",\"is_error\":false,\"duration_ms\":1250,\"session_id\":\"session-1\",\"usage\":{\"input_tokens\":10,\"cache_read_input_tokens\":20,\"cache_creation_input_tokens\":30,\"output_tokens\":40}}");
        const string standardError = "stderr sintetico";

        ClaudeCliResult result = ClaudeCliClient.ParseOutput(
            output,
            standardError,
            exitCode: 0,
            startError: null);

        Assert.Equal("Risposta", result.Response);
        Assert.False(result.IsError);
        Assert.Equal(1250, result.DurationMs);
        Assert.Equal("session-1", result.SessionId);
        Assert.Equal("model-a", result.EffectiveModel);
        Assert.Equal(10, result.InputTokens);
        Assert.Equal(20, result.CacheReadInputTokens);
        Assert.Equal(30, result.CacheCreationInputTokens);
        Assert.Equal(40, result.OutputTokens);
        Assert.Equal(60, result.TotalInputTokens);
        Assert.Equal(100, result.TotalTokens);
        Assert.Null(result.StartError);
        Assert.Null(result.ParsingError);
        Assert.Equal(output, result.RawOutput);
        Assert.Equal(standardError, result.StandardError);
        Assert.Equal(0, result.ExitCode);
    }
}
