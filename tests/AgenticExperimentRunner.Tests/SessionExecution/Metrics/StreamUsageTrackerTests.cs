using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class StreamUsageTrackerTests
{
    [Fact]
    public void Observe_RepeatedAssistantEventsOfSameMessage_AreCountedOnceWithMaximum()
    {
        var tracker = new StreamUsageTracker();

        tracker.Observe(FakeClaudeCli.Assistant("msg_1", 2, 100, 50, 8));
        tracker.Observe(FakeClaudeCli.Assistant("msg_1", 2, 100, 50, 32));
        tracker.Observe(FakeClaudeCli.Assistant("msg_2", 2, 150, 10, 5));

        Assert.Equal(2, tracker.Messages.Count);
        Assert.Equal(new TokenUsage(2, 100, 50, 32), tracker.Messages[0].Usage);
        Assert.False(tracker.Messages[0].OutputIsFinal);
        Assert.Equal(new TokenUsage(4, 250, 60, 37), tracker.ObservedUsage);
        Assert.Equal(351, tracker.ObservedUsage.TotalTokens);
    }

    [Fact]
    public void Observe_MessageDelta_ProvidesFinalOutputOfOpenMessage()
    {
        var tracker = new StreamUsageTracker();

        tracker.Observe("""{"type":"stream_event","event":{"type":"message_start","message":{"id":"msg_1","model":"m","usage":{"input_tokens":2,"cache_read_input_tokens":100,"cache_creation_input_tokens":5,"output_tokens":1}}}}""");
        tracker.Observe(FakeClaudeCli.Assistant("msg_1", 2, 100, 5, 9));
        tracker.Observe("""{"type":"stream_event","event":{"type":"message_delta","usage":{"output_tokens":420}}}""");
        // Un evento assistant successivo con output parziale non deve abbassare il valore finale.
        tracker.Observe(FakeClaudeCli.Assistant("msg_1", 2, 100, 5, 9));

        MessageUsage message = Assert.Single(tracker.Messages);
        Assert.True(message.OutputIsFinal);
        Assert.Equal(new TokenUsage(2, 100, 5, 420), message.Usage);
        Assert.Equal("m", message.Model);
    }

    [Fact]
    public void Observe_Initialization_KeepsFirstInitOnly()
    {
        var tracker = new StreamUsageTracker();

        Assert.Equal(StreamLineKind.Initialization, tracker.Observe(FakeClaudeCli.Init()));
        tracker.Observe(FakeClaudeCli.Init("Bash"));

        Assert.Equal(FakeClaudeCli.Model, tracker.Initialization!.Model);
        Assert.Equal("9.9.9", tracker.Initialization.CliVersion);
        Assert.DoesNotContain("Bash", tracker.Initialization.Tools);
        Assert.Equal([new McpServerState("benchmark_workspace", "connected")], tracker.Initialization.McpServers);
    }

    [Fact]
    public void Observe_ToolUsesAndErrors_AreDeduplicatedById()
    {
        var tracker = new StreamUsageTracker();

        tracker.Observe(FakeClaudeCli.Assistant("msg_1", 1, 1, 1, 1, toolUseId: "tool_1"));
        tracker.Observe(FakeClaudeCli.Assistant("msg_1", 1, 1, 1, 2, toolUseId: "tool_1"));
        tracker.Observe(FakeClaudeCli.Assistant("msg_2", 1, 1, 1, 1, toolUseId: "tool_2"));
        tracker.Observe(FakeClaudeCli.ToolResult("tool_1", isError: true));
        tracker.Observe(FakeClaudeCli.ToolResult("tool_1", isError: true));
        tracker.Observe(FakeClaudeCli.ToolResult("tool_2", isError: false));

        Assert.Equal(2, tracker.ToolCalls["mcp__benchmark_workspace__read_file"]);
        Assert.Equal(1, tracker.ToolErrorCount);
    }

    [Fact]
    public void Observe_Result_ReadsAuthoritativeUsageIterationsAndModels()
    {
        var tracker = new StreamUsageTracker();

        StreamLineKind kind = tracker.Observe(
            """{"type":"result","subtype":"success","is_error":false,"terminal_reason":"completed","num_turns":17,"duration_ms":27154,"total_cost_usd":0.0717909,"permission_denials":[{"x":1}],"usage":{"input_tokens":26,"cache_creation_input_tokens":3419,"cache_read_input_tokens":55807,"output_tokens":1650,"iterations":[{"input_tokens":2,"output_tokens":171,"cache_read_input_tokens":5647,"cache_creation_input_tokens":349}]},"modelUsage":{"claude-sonnet-5-5":{}}}""");

        Assert.Equal(StreamLineKind.Result, kind);
        SessionResult result = tracker.Result!;
        Assert.Equal(new TokenUsage(26, 55807, 3419, 1650), result.Usage);
        Assert.Equal([new TokenUsage(2, 5647, 349, 171)], result.Iterations);
        Assert.Equal(["claude-sonnet-5-5"], result.Models);
        Assert.Equal("success", result.Subtype);
        Assert.False(result.IsError);
        Assert.Equal(17, result.NumTurns);
        Assert.Equal(0.0717909m, result.ReportedCostUsd);
        Assert.Equal(1, result.PermissionDenialCount);
    }

    [Theory]
    [InlineData("non json")]
    [InlineData("[1,2]")]
    [InlineData("")]
    public void Observe_InvalidLines_AreCountedAndNotFatal(string line)
    {
        var tracker = new StreamUsageTracker();

        Assert.Equal(StreamLineKind.Malformed, tracker.Observe(line));

        Assert.Equal(1, tracker.MalformedLineCount);
        Assert.Equal(1, tracker.LineCount);
    }

    [Fact]
    public void Observe_OtherEvents_DoNotChangeUsage()
    {
        var tracker = new StreamUsageTracker();

        Assert.Equal(StreamLineKind.Other, tracker.Observe("""{"type":"rate_limit_event","rate_limit_info":{}}"""));
        Assert.Equal(StreamLineKind.Other, tracker.Observe("""{"type":"system","subtype":"thinking_tokens","estimated_tokens":50}"""));

        Assert.Equal(TokenUsage.Zero, tracker.ObservedUsage);
    }
}
