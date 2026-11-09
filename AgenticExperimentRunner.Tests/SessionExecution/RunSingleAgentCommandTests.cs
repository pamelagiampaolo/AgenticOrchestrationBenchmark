using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class RunSingleAgentCommandTests
{
    private static readonly string[] ValidArguments =
    [
        "--prepared-run", @"C:\AgenticAIResults\r1\prepared-run.json",
        "--claude", @"C:\bin\claude.exe",
        "--mcp-server", @"C:\bin\server.exe",
        "--model", "claude-sonnet-5-5",
        "--time-limit-seconds", "900",
        "--token-budget", "2000000",
    ];

    [Fact]
    public void TryParse_ValidArguments_BuildsRequest()
    {
        Assert.True(RunSingleAgentCommand.TryParse(ValidArguments, out SingleAgentSessionRequest? request, out string? error));

        Assert.Null(error);
        Assert.Equal(TimeSpan.FromSeconds(900), request!.TimeLimit);
        Assert.Equal(2_000_000, request.TokenBudget);
        Assert.Equal("claude-sonnet-5-5", request.Model);
    }

    [Theory]
    [InlineData("--time-limit-seconds", "0")]
    [InlineData("--time-limit-seconds", "-5")]
    [InlineData("--time-limit-seconds", "1.5")]
    [InlineData("--token-budget", "abc")]
    [InlineData("--token-budget", "0")]
    [InlineData("--claude", "claude.exe")]
    [InlineData("--prepared-run", "prepared-run.json")]
    public void TryParse_InvalidValue_IsRejected(string option, string value)
    {
        string[] arguments = [.. ValidArguments];
        arguments[Array.IndexOf(arguments, option) + 1] = value;

        Assert.False(RunSingleAgentCommand.TryParse(arguments, out _, out string? error));
        Assert.Contains(option, error);
    }

    [Fact]
    public async Task ExecuteAsync_MissingOption_ReturnsUsageErrorWithEmptyOutput()
    {
        var command = new RunSingleAgentCommand(new SingleAgentSessionRunner(TimeProvider.System, AppContext.BaseDirectory));
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await command.ExecuteAsync(ValidArguments[..^2], output, error, TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("--token-budget", error.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_MissingFiles_ReturnsSessionFailed()
    {
        var command = new RunSingleAgentCommand(new SingleAgentSessionRunner(TimeProvider.System, AppContext.BaseDirectory));
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await command.ExecuteAsync(ValidArguments, output, error, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.StartsWith("Sessione non eseguita:", error.ToString());
    }
}
