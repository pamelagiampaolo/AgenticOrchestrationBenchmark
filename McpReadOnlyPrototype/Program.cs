using System.Text.Json;
using McpReadOnlyPrototype;

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]))
{
    Console.Error.WriteLine("Uso: McpReadOnlyPrototype <root-assoluta-consentita>");
    return 2;
}

RootedFileAccess fileAccess;
try
{
    fileAccess = new RootedFileAccess(args[0]);
}
catch (Exception exception) when (exception is ArgumentException or DirectoryNotFoundException or IOException)
{
    Console.Error.WriteLine($"Configurazione non valida: {exception.Message}");
    return 2;
}

while (await Console.In.ReadLineAsync() is { } line)
{
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    string response = HandleRequest(line, fileAccess);
    await Console.Out.WriteLineAsync(response);
    await Console.Out.FlushAsync();
}

return 0;

static string HandleRequest(string requestJson, RootedFileAccess fileAccess)
{
    JsonElement responseId = default;
    try
    {
        using JsonDocument request = JsonDocument.Parse(requestJson);
        JsonElement root = request.RootElement;
        responseId = root.TryGetProperty("id", out JsonElement requestId)
            ? requestId.Clone()
            : default;
        string? method = root.TryGetProperty("method", out JsonElement methodElement)
            ? methodElement.GetString()
            : null;

        if (responseId.ValueKind == JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        object result = method switch
        {
            "initialize" => new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { tools = new { } },
                serverInfo = new { name = "readonly-prototype", version = "0.1.0" }
            },
            "ping" => new { },
            "tools/list" => CreateToolList(),
            "tools/call" => CallTool(root, fileAccess),
            _ => throw new McpRequestException(-32601, "Metodo MCP non supportato.")
        };

        return JsonSerializer.Serialize(new { jsonrpc = "2.0", id = responseId, result });
    }
    catch (JsonException)
    {
        return SerializeError(default, -32700, "Richiesta JSON non valida.");
    }
    catch (McpRequestException exception)
    {
        return SerializeError(responseId, exception.Code, exception.Message);
    }
    catch (Exception exception) when (exception is AccessDeniedException
        or FileNotFoundException
        or DirectoryNotFoundException
        or UnauthorizedAccessException
        or IOException)
    {
        return SerializeError(responseId, -32001, exception.Message);
    }
}

static object CreateToolList() => new
{
    tools = new object[]
    {
        new
        {
            name = "list_files",
            description = "Elenca le voci immediate di una directory nella root consentita.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    path = new { type = "string", description = "Percorso relativo; vuoto per la root." }
                },
                required = new[] { "path" },
                additionalProperties = false
            }
        },
        new
        {
            name = "read_file",
            description = "Legge un file UTF-8 nella root consentita.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    path = new { type = "string", description = "Percorso relativo del file." }
                },
                required = new[] { "path" },
                additionalProperties = false
            }
        }
    }
};

static object CallTool(JsonElement request, RootedFileAccess fileAccess)
{
    JsonElement parameters = GetRequiredProperty(request, "params");
    string toolName = GetRequiredProperty(parameters, "name").GetString()
        ?? throw new McpRequestException(-32602, "Nome dello strumento mancante.");
    JsonElement arguments = GetRequiredProperty(parameters, "arguments");
    string path = GetRequiredProperty(arguments, "path").GetString()
        ?? throw new McpRequestException(-32602, "Percorso mancante.");

    string text = toolName switch
    {
        "list_files" => JsonSerializer.Serialize(fileAccess.ListFiles(path)),
        "read_file" => fileAccess.ReadFile(path),
        _ => throw new McpRequestException(-32602, "Strumento non disponibile.")
    };

    return new { content = new[] { new { type = "text", text } }, isError = false };
}

static JsonElement GetRequiredProperty(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out JsonElement property))
    {
        throw new McpRequestException(-32602, $"Proprietà obbligatoria mancante: {propertyName}.");
    }

    return property;
}

static string SerializeError(JsonElement id, int code, string message) => JsonSerializer.Serialize(new
{
    jsonrpc = "2.0",
    id = id.ValueKind == JsonValueKind.Undefined ? (object?)null : id,
    error = new { code, message }
});

file sealed class McpRequestException : Exception
{
    public McpRequestException(int code, string message)
        : base(message)
    {
        Code = code;
    }

    public int Code { get; }
}
