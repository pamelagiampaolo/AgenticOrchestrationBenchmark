using System.Text.Json;
using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class ClaudeLaunchProfileTests
{
    [Fact]
    public void BuildArguments_MatchesVerifiedProfileWithoutPrompt()
    {
        IReadOnlyList<string> arguments = ClaudeLaunchProfile.BuildArguments(
            "claude-sonnet-5-5", @"C:\r\mcp.json", @"C:\r\settings.json", @"C:\r\append.md");

        Assert.Equal(
            [
                "-p",
                "--model", "claude-sonnet-5-5",
                "--output-format", "stream-json",
                "--verbose",
                "--include-partial-messages",
                "--include-hook-events",
                "--restricted",
                "--strict-mcp-config",
                "--mcp-config", @"C:\r\mcp.json",
                "--settings", @"C:\r\settings.json",
                "--setting-sources", "",
                "--tools", "",
                "--allowedTools",
                "mcp__benchmark_workspace__list_files,mcp__benchmark_workspace__read_file,mcp__benchmark_workspace__write_file,mcp__benchmark_workspace__delete_file,mcp__benchmark_workspace__move_file",
                "--disallowedTools", "Bash,PowerShell,Read,Write,Edit,Glob,Grep,WebFetch,WebSearch,Task,Agent,Skill",
                "--disable-slash-commands",
                "--no-session-persistence",
                "--no-chrome",
                "--permission-mode", "dontAsk",
                "--permission-prompts", "none",
                "--append-system-prompt-file", @"C:\r\append.md",
            ],
            arguments);
    }

    [Fact]
    public void BuildSettingsJson_DisablesHooksAgentsAndClaudeAiServers()
    {
        using JsonDocument settings = JsonDocument.Parse(ClaudeLaunchProfile.BuildSettingsJson());

        Assert.True(settings.RootElement.GetProperty("disableAllHooks").GetBoolean());
        JsonElement environment = settings.RootElement.GetProperty("env");
        Assert.Equal("1", environment.GetProperty("CLAUDE_AGENT_SDK_DISABLE_BUILTIN_AGENTS").GetString());
        Assert.Equal("false", environment.GetProperty("ENABLE_CLAUDEAI_MCP_SERVERS").GetString());
    }

    [Fact]
    public void BuildMcpConfigurationJson_DeclaresOnlyWorkspaceServerWithWritablePolicy()
    {
        using JsonDocument configuration = JsonDocument.Parse(ClaudeLaunchProfile.BuildMcpConfigurationJson(
            @"C:\server\McpReadOnlyPrototype.exe", @"C:\AgenticAIRuns\r1\agent-root", ["workspace", "agent-tests"]));

        JsonElement servers = configuration.RootElement.GetProperty("mcpServers");
        JsonElement server = Assert.Single(servers.EnumerateObject()).Value;
        Assert.Equal("benchmark_workspace", servers.EnumerateObject().Single().Name);
        Assert.Equal(@"C:\server\McpReadOnlyPrototype.exe", server.GetProperty("command").GetString());
        Assert.Equal([@"C:\AgenticAIRuns\r1\agent-root"], server.GetProperty("args").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "workspace" + Path.PathSeparator + "agent-tests",
            server.GetProperty("env").GetProperty("MCP_WRITABLE_DIRECTORIES").GetString());
    }

    [Fact]
    public void FindConfigurationViolations_ExpectedInit_HasNone()
    {
        Assert.Empty(ClaudeLaunchProfile.FindConfigurationViolations(Init(), FakeClaudeCli.Model));
    }

    [Fact]
    public void FindConfigurationViolations_ReportsEveryDeviation()
    {
        SessionInitialization initialization = Init() with
        {
            Model = "claude-opus-5-5",
            Tools = [.. ClaudeLaunchProfile.QualifiedMcpToolNames.Skip(1), "Bash"],
            McpServers = [new McpServerState("benchmark_workspace", "failed"), new McpServerState("altro", "connected")],
            Agents = ["general-purpose"],
            Skills = ["skill"],
            SlashCommands = ["/x"],
        };

        IReadOnlyList<string> violations = ClaudeLaunchProfile.FindConfigurationViolations(initialization, "claude-sonnet-5-5");

        Assert.Equal(6, violations.Count);
        Assert.Contains(violations, violation => violation.StartsWith("modello 'claude-opus-5-5'", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("Bash", StringComparison.Ordinal));
    }

    [Fact]
    public void RemoveInheritedAgentVariables_RemovesOnlyAgentPrefixesAndReturnsNames()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CLAUDECODE"] = "1",
            ["claude_config_dir"] = "x",
            ["ANTHROPIC_API_KEY"] = "segreto",
            ["PATH"] = @"C:\Windows",
            ["MY_CLAUDE"] = "resta",
        };

        IReadOnlyList<string> removed = ChildProcessEnvironment.RemoveInheritedAgentVariables(environment);

        Assert.Equal(["ANTHROPIC_API_KEY", "CLAUDECODE", "claude_config_dir"], removed);
        Assert.Equal(["MY_CLAUDE", "PATH"], environment.Keys.Order(StringComparer.Ordinal));
    }

    private static SessionInitialization Init()
    {
        var tracker = new StreamUsageTracker();
        tracker.Observe(FakeClaudeCli.Init());
        return tracker.Initialization!;
    }
}
