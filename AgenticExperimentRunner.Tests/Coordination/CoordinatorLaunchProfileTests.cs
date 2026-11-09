using System.Text.Json;
using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class CoordinatorLaunchProfileTests
{
    private static readonly string[] CoordinatorTools =
        [.. ClaudeLaunchProfile.QualifiedMcpToolNames, ClaudeLaunchProfile.QualifiedDelegateToolName];

    private static readonly string[] CoordinatorServers =
        [ClaudeLaunchProfile.McpServerName, ClaudeLaunchProfile.CoordinationServerName];

    [Fact]
    public void BuildSettingsJson_DisablesAutomaticBackgroundingOfMcpCalls()
    {
        using JsonDocument settings = JsonDocument.Parse(ClaudeLaunchProfile.BuildSettingsJson());

        Assert.Equal("0", settings.RootElement.GetProperty("env").GetProperty("CLAUDE_CODE_MCP_AUTO_BACKGROUND_MS").GetString());
        Assert.DoesNotContain("\r", ClaudeLaunchProfile.BuildSettingsJson());
    }

    [Fact]
    public void BuildCoordinatorMcpConfigurationJson_DeclaresWorkspaceAndRelayWithTimeout()
    {
        string json = ClaudeLaunchProfile.BuildCoordinatorMcpConfigurationJson(
            @"C:\server\McpReadOnlyPrototype.exe",
            @"C:\AgenticAIRuns\r1\agent-root",
            ["workspace", "agent-tests"],
            new McpDevelopmentToolchain(@"C:\bin\node.exe", @"C:\toolchain\vitest.mjs", @"C:\toolchain\tsc.js"),
            @"C:\AgenticAIResults\r1\mcp-worker",
            @"C:\AgenticAIResults\r1\mcp-audit",
            @"C:\runner\AgenticExperimentRunner.exe",
            "agentic-coordination-abc",
            TimeSpan.FromSeconds(1800.5));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement servers = document.RootElement.GetProperty("mcpServers");
        Assert.Equal(["benchmark_workspace", "benchmark_coordination"], servers.EnumerateObject().Select(server => server.Name));
        JsonElement relay = servers.GetProperty("benchmark_coordination");
        Assert.Equal(@"C:\runner\AgenticExperimentRunner.exe", relay.GetProperty("command").GetString());
        Assert.Equal(["coordination-server", "--pipe", "agentic-coordination-abc"], relay.GetProperty("args").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(1_800_500, relay.GetProperty("timeout").GetInt64());
        Assert.Equal(
            "workspace;agent-tests",
            servers.GetProperty("benchmark_workspace").GetProperty("env").GetProperty("MCP_WRITABLE_DIRECTORIES").GetString());
        Assert.DoesNotContain("\r", json);
    }

    [Fact]
    public void BuildArguments_WithCoordinatorTools_AllowsDelegateTool()
    {
        IReadOnlyList<string> arguments = ClaudeLaunchProfile.BuildArguments("m", "mcp.json", "settings.json", "append.md", CoordinatorTools);

        string allowed = arguments[arguments.ToList().IndexOf("--allowedTools") + 1];
        Assert.Contains(ClaudeLaunchProfile.QualifiedDelegateToolName, allowed.Split(','));
        Assert.Equal(10, allowed.Split(',').Length);
    }

    [Fact]
    public void FindConfigurationViolations_CoordinatorWithExpectedToolsAndServers_HasNone()
    {
        Assert.Empty(ClaudeLaunchProfile.FindConfigurationViolations(
            Initialization(CoordinatorTools, CoordinatorServers.Select(name => new McpServerState(name, "connected"))),
            "m",
            CoordinatorTools,
            CoordinatorServers));
    }

    [Fact]
    public void FindConfigurationViolations_RelayNotConnected_IsReported()
    {
        IReadOnlyList<string> violations = ClaudeLaunchProfile.FindConfigurationViolations(
            Initialization(
                CoordinatorTools,
                [new McpServerState(ClaudeLaunchProfile.McpServerName, "connected"), new McpServerState(ClaudeLaunchProfile.CoordinationServerName, "failed")]),
            "m",
            CoordinatorTools,
            CoordinatorServers);

        Assert.Contains("benchmark_coordination:failed", Assert.Single(violations));
    }

    [Fact]
    public void FindConfigurationViolations_WorkerExposedToDelegateTool_IsReported()
    {
        IReadOnlyList<string> violations = ClaudeLaunchProfile.FindConfigurationViolations(
            Initialization(CoordinatorTools, [new McpServerState(ClaudeLaunchProfile.McpServerName, "connected")]),
            "m");

        Assert.Contains("delegate_task", Assert.Single(violations));
    }

    private static SessionInitialization Initialization(IEnumerable<string> tools, IEnumerable<McpServerState> servers) =>
        new("m", "9.9.9", "s", tools.ToList(), servers.ToList(), [], [], [], "dontAsk");
}
