using AgenticExperimentRunner.Coordination;
using Xunit;

public sealed class RunStaticHierarchyCommandTests
{
    private const string Relay = @"C:\runner\AgenticExperimentRunner.exe";

    private static readonly string[] ValidArguments =
    [
        "--prepared-run", @"C:\AgenticAIResults\r1\prepared-run.json",
        "--claude", @"C:\cli\claude.exe",
        "--mcp-server", @"C:\server\McpReadOnlyPrototype.exe",
        "--node", @"C:\bin\node.exe",
        "--vitest", @"C:\toolchain\vitest.mjs",
        "--typescript", @"C:\toolchain\tsc.js",
        "--model", "claude-sonnet-5-5",
        "--time-limit-seconds", "600",
        "--token-budget", "500000",
    ];

    [Fact]
    public void TryParse_ValidArguments_FixesThreeDelegations()
    {
        Assert.True(RunStaticHierarchyCommand.TryParse(ValidArguments, Relay, out HierarchicalRunRequest? request, out string? error));

        Assert.Null(error);
        Assert.Equal(Relay, request!.CoordinationServerPath);
        Assert.Equal(3, request.MaxDelegations);
        Assert.Equal(TimeSpan.FromSeconds(600), request.TimeLimit);
        Assert.Equal(500_000, request.TokenBudget);
    }

    [Theory]
    [InlineData("--time-limit-seconds", "0")]
    [InlineData("--time-limit-seconds", "1.5")]
    [InlineData("--token-budget", "0")]
    [InlineData("--node", "node.exe")]
    [InlineData("--vitest", "vitest.mjs")]
    public void TryParse_InvalidValue_IsRejected(string option, string value)
    {
        string[] arguments = (string[])ValidArguments.Clone();
        arguments[Array.IndexOf(arguments, option) + 1] = value;

        Assert.False(RunStaticHierarchyCommand.TryParse(arguments, Relay, out _, out string? error));
        Assert.Contains(option, error);
    }

    [Fact]
    public void TryParse_MaxDelegationsOption_IsRejected()
    {
        string[] arguments = [.. ValidArguments, "--max-delegations", "4"];

        Assert.False(RunStaticHierarchyCommand.TryParse(arguments, Relay, out _, out string? error));
        Assert.Contains("--max-delegations", error);
    }

    [Fact]
    public async Task ExecuteAsync_MissingFiles_ReturnsRunFailed()
    {
        var command = new RunStaticHierarchyCommand(
            HierarchicalRunRunner.CreateStatic(TimeProvider.System, AppContext.BaseDirectory),
            Relay);
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await command.ExecuteAsync(
            ValidArguments,
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(RunStaticHierarchyCommand.RunFailedExitCode, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.StartsWith("Run non eseguita:", error.ToString());
    }
}
