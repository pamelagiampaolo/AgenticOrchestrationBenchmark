using System.Text.Json;
using AgenticExperimentRunner.Coordination;
using Xunit;

public sealed class CoordinationMcpServerTests
{
    private readonly List<string> delegatedTasks = [];

    [Fact]
    public async Task Initialize_DeclaresProtocolToolsCapabilityAndServerName()
    {
        JsonElement result = await CallForResultAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""");

        Assert.Equal("2025-06-18", result.GetProperty("protocolVersion").GetString());
        Assert.Equal(JsonValueKind.Object, result.GetProperty("capabilities").GetProperty("tools").ValueKind);
        Assert.Equal("benchmark-coordination", result.GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Ping_ReturnsEmptyObject()
    {
        JsonElement result = await CallForResultAsync("""{"jsonrpc":"2.0","id":"p","method":"ping"}""");

        Assert.Empty(result.EnumerateObject());
    }

    [Theory]
    [InlineData("""{"jsonrpc":"2.0","method":"notifications/initialized"}""")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NotificationsAndBlankLines_GetNoResponse(string line)
    {
        Assert.Null(await CreateServer().HandleLineAsync(line, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ToolsList_ExposesOnlyDelegateTaskWithSingleRequiredStringParameter()
    {
        JsonElement result = await CallForResultAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");

        JsonElement tool = Assert.Single(result.GetProperty("tools").EnumerateArray());
        Assert.Equal("delegate_task", tool.GetProperty("name").GetString());
        JsonElement schema = tool.GetProperty("inputSchema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["task"], schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        JsonElement task = Assert.Single(schema.GetProperty("properties").EnumerateObject()).Value;
        Assert.Equal("string", task.GetProperty("type").GetString());
        Assert.Equal(CoordinationMcpServer.MaxTaskLength, task.GetProperty("maxLength").GetInt32());
    }

    [Fact]
    public async Task ToolsCall_ValidTask_IsDelegatedAndHandlerTextReturned()
    {
        JsonElement result = await CallForResultAsync(ToolCall(3, """{"task":"Scrivi la funzione è richiesta"}"""));

        Assert.Equal(["Scrivi la funzione è richiesta"], delegatedTasks);
        Assert.False(result.GetProperty("isError").GetBoolean());
        JsonElement content = Assert.Single(result.GetProperty("content").EnumerateArray());
        Assert.Equal("text", content.GetProperty("type").GetString());
        Assert.Equal("esito di: Scrivi la funzione è richiesta", content.GetProperty("text").GetString());
    }

    [Theory]
    [InlineData(DelegationStatus.Failed)]
    [InlineData(DelegationStatus.Rejected)]
    public async Task ToolsCall_NotCompletedOutcome_IsMarkedAsError(DelegationStatus status)
    {
        var server = new CoordinationMcpServer((_, _) => Task.FromResult(new DelegationOutcome(status, "motivo")));

        string? response = await server.HandleLineAsync(ToolCall(4, """{"task":"t"}"""), TestContext.Current.CancellationToken);

        using JsonDocument document = JsonDocument.Parse(response!);
        Assert.True(document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [Theory]
    [InlineData("""{"jsonrpc":"2.0","id":5,"method":"tools/call"}""")]
    [InlineData("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"delegate_task"}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"delegate_task","arguments":"t"}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"delegate_task","arguments":{}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"delegate_task","arguments":{"task":"t","role":"x"}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"delegate_task","arguments":{"task":7}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"delegate_task","arguments":{"task":""}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"delegate_task","arguments":{"task":" \n "}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"read_file","arguments":{"task":"t"}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"arguments":{"task":"t"}}}""")]
    public async Task ToolsCall_InvalidParameters_AreRejectedWithoutDelegating(string line)
    {
        await AssertErrorAsync(line, -32602, expectedId: 5);

        Assert.Empty(delegatedTasks);
    }

    [Fact]
    public async Task ToolsCall_TaskLongerThanMaximum_IsRejectedWithoutDelegating()
    {
        string task = new('x', CoordinationMcpServer.MaxTaskLength + 1);

        await AssertErrorAsync(ToolCall(6, JsonSerializer.Serialize(new { task })), -32602, expectedId: 6);

        Assert.Empty(delegatedTasks);
    }

    [Fact]
    public async Task ToolsCall_TaskOfMaximumLength_IsDelegated()
    {
        string task = new('x', CoordinationMcpServer.MaxTaskLength);

        await CallForResultAsync(ToolCall(7, JsonSerializer.Serialize(new { task })));

        Assert.Single(delegatedTasks);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        await AssertErrorAsync("""{"jsonrpc":"2.0","id":8,"method":"resources/list"}""", -32601, expectedId: 8);
    }

    [Theory]
    [InlineData("{non json")]
    [InlineData("[1,2]")]
    public async Task MalformedRequest_ReturnsErrorWithNullId(string line)
    {
        string? response = await CreateServer().HandleLineAsync(line, TestContext.Current.CancellationToken);

        using JsonDocument document = JsonDocument.Parse(response!);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("id").ValueKind);
        Assert.True(document.RootElement.GetProperty("error").GetProperty("code").GetInt32() is -32700 or -32600);
    }

    [Fact]
    public async Task ChannelFailure_IsReportedAsJsonRpcErrorForThatRequest()
    {
        var server = new CoordinationMcpServer((_, _) => throw new CoordinationChannelException("pipe chiusa"));

        string? response = await server.HandleLineAsync(ToolCall(9, """{"task":"t"}"""), TestContext.Current.CancellationToken);

        using JsonDocument document = JsonDocument.Parse(response!);
        Assert.Equal(9, document.RootElement.GetProperty("id").GetInt32());
        JsonElement error = document.RootElement.GetProperty("error");
        Assert.Equal(CoordinationMcpServer.ChannelUnavailableErrorCode, error.GetProperty("code").GetInt32());
        Assert.Contains("pipe chiusa", error.GetProperty("message").GetString());
    }

    internal static string ToolCall(int id, string argumentsJson) =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id
        + ",\"method\":\"tools/call\",\"params\":{\"name\":\"delegate_task\",\"arguments\":" + argumentsJson + "}}";

    private CoordinationMcpServer CreateServer() => new((task, _) =>
    {
        delegatedTasks.Add(task);
        return Task.FromResult(new DelegationOutcome(DelegationStatus.Completed, $"esito di: {task}"));
    });

    private async Task<JsonElement> CallForResultAsync(string line)
    {
        string? response = await CreateServer().HandleLineAsync(line, TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        using JsonDocument document = JsonDocument.Parse(response);
        Assert.Equal("2.0", document.RootElement.GetProperty("jsonrpc").GetString());
        Assert.False(document.RootElement.TryGetProperty("error", out _), response);
        return document.RootElement.GetProperty("result").Clone();
    }

    private async Task AssertErrorAsync(string line, int expectedCode, int expectedId)
    {
        string? response = await CreateServer().HandleLineAsync(line, TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        using JsonDocument document = JsonDocument.Parse(response);
        Assert.Equal(expectedId, document.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }
}
