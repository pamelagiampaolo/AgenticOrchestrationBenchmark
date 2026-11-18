using AgenticExperimentRunner.Coordination;
using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class RunAdaptiveHierarchyCommandTests
{
    private const string Relay = @"C:\runner\AgenticExperimentRunner.exe";

    private static readonly string[] ValidArguments =
    [
        "--prepared-run", @"C:\AgenticAIResults\r1\prepared-run.json",
        "--claude", @"C:\cli\claude.exe",
        "--mcp-server", @"C:\server\AgentWorkspaceMcpServer.exe",
        "--node", @"C:\bin\node.exe",
        "--vitest", @"C:\toolchain\vitest.mjs",
        "--typescript", @"C:\toolchain\tsc.js",
        "--model", "claude-sonnet-5-5",
        "--time-limit-seconds", "600",
        "--token-budget", "500000",
        "--max-delegations", "10",
        "--run-kind", "official",
        "--repetition", "3",
        "--order-seed", "official-seed",
        "--order-position", "54",
    ];

    [Fact]
    public void TryParse_ValidArguments_BuildRequestWithRelayPath()
    {
        Assert.True(RunAdaptiveHierarchyCommand.TryParse(ValidArguments, Relay, out HierarchicalRunRequest? request, out _));

        Assert.Equal(Relay, request!.CoordinationServerPath);
        Assert.Equal(TimeSpan.FromSeconds(600), request.TimeLimit);
        Assert.Equal(500_000, request.TokenBudget);
        Assert.Equal(10, request.MaxDelegations);
        Assert.Equal("claude-sonnet-5-5", request.Model);
        Assert.Equal(ExperimentRunKind.Official, request.ExperimentMetadata.Kind);
        Assert.Equal(3, request.ExperimentMetadata.Repetition);
        Assert.Equal("official-seed", request.ExperimentMetadata.OrderSeed);
        Assert.Equal(54, request.ExperimentMetadata.OrderPosition);
    }

    [Theory]
    [InlineData("--max-delegations", "0")]
    [InlineData("--max-delegations", "-1")]
    [InlineData("--max-delegations", "dieci")]
    [InlineData("--max-delegations", "3000000000")]
    [InlineData("--token-budget", "0")]
    [InlineData("--time-limit-seconds", "1.5")]
    [InlineData("--claude", "claude.exe")]
    [InlineData("--model", "claude-opus-5-5")]
    [InlineData("--run-kind", "Official")]
    [InlineData("--repetition", "0")]
    [InlineData("--order-position", "-1")]
    public void TryParse_InvalidValue_IsRejected(string option, string value)
    {
        string[] arguments = (string[])ValidArguments.Clone();
        arguments[Array.IndexOf(arguments, option) + 1] = value;

        Assert.False(RunAdaptiveHierarchyCommand.TryParse(arguments, Relay, out _, out string? error));
        Assert.Contains(option, error);
    }

    [Fact]
    public void TryParse_MissingMaxDelegations_IsRejected()
    {
        string[] arguments = [.. ValidArguments[..^10], .. ValidArguments[^8..]];
        Assert.False(RunAdaptiveHierarchyCommand.TryParse(arguments, Relay, out _, out string? error));
        Assert.Contains("--max-delegations", error);
    }
}
