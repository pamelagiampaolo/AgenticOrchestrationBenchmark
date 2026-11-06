using System.Text.Json;
using McpReadOnlyPrototype;

if (args.Length is < 1 or > 2
    || !Path.IsPathFullyQualified(args[0])
    || (args.Length == 2 && !Path.IsPathFullyQualified(args[1])))
{
    Console.Error.WriteLine("Uso: McpReadOnlyPrototype <root-assoluta-consentita> [runner-P2.2-assoluto]");
    return 2;
}

RootedFileAccess fileAccess;
PublicSuiteRunner? publicSuiteRunner = null;
try
{
    fileAccess = new RootedFileAccess(args[0]);
    if (args.Length == 2)
    {
        int timeoutSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("MCP_PUBLIC_SUITE_TIMEOUT_SECONDS"),
            out int configuredTimeoutSeconds)
            ? configuredTimeoutSeconds
            : 300;
        publicSuiteRunner = new PublicSuiteRunner(args[1], TimeSpan.FromSeconds(timeoutSeconds));
    }
}
catch (Exception exception) when (exception is ArgumentException
    or DirectoryNotFoundException
    or FileNotFoundException
    or IOException)
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

    string response = await HandleRequestAsync(line, fileAccess, publicSuiteRunner);
    await Console.Out.WriteLineAsync(response);
    await Console.Out.FlushAsync();
}

return 0;

static async Task<string> HandleRequestAsync(
    string requestJson,
    RootedFileAccess fileAccess,
    PublicSuiteRunner? publicSuiteRunner)
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
            "tools/list" => CreateToolList(publicSuiteRunner is not null),
            "tools/call" => await CallToolAsync(root, fileAccess, publicSuiteRunner),
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
    catch (PublicSuiteRunException exception)
    {
        return SerializeError(responseId, -32002, exception.Message);
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

static object CreateToolList(bool includePublicSuiteRunner)
{
    object[] readOnlyTools =
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
    };
    object publicSuiteTool = new
    {
        name = "run_public_suite",
        description = "Avvia la suite pubblica comune nel runner Windows Sandbox P2.2 predefinito.",
        inputSchema = new
        {
            type = "object",
            properties = new { },
            additionalProperties = false
        }
    };

    return new
    {
        tools = includePublicSuiteRunner
            ? readOnlyTools.Append(publicSuiteTool).ToArray()
            : readOnlyTools
    };
}

static async Task<object> CallToolAsync(
    JsonElement request,
    RootedFileAccess fileAccess,
    PublicSuiteRunner? publicSuiteRunner)
{
    JsonElement parameters = GetRequiredProperty(request, "params");
    string toolName = GetRequiredProperty(parameters, "name").GetString()
        ?? throw new McpRequestException(-32602, "Nome dello strumento mancante.");
    JsonElement arguments = GetRequiredProperty(parameters, "arguments");
    if (arguments.ValueKind != JsonValueKind.Object)
    {
        throw new McpRequestException(-32602, "Gli argomenti devono essere un oggetto JSON.");
    }

    if (toolName == "run_public_suite")
    {
        EnsureExactProperties(arguments, Array.Empty<string>());
        if (publicSuiteRunner is null)
        {
            throw new McpRequestException(-32602, "Strumento non disponibile.");
        }
        PublicSuiteResult result = await publicSuiteRunner.RunAsync();
        return new
        {
            content = new[] { new { type = "text", text = JsonSerializer.Serialize(result) } },
            structuredContent = result,
            isError = false
        };
    }

    EnsureExactProperties(arguments, new[] { "path" });
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

static void EnsureExactProperties(JsonElement element, IReadOnlyCollection<string> expectedNames)
{
    string[] actualNames = element.EnumerateObject().Select(property => property.Name).ToArray();
    if (actualNames.Length != expectedNames.Count
        || actualNames.Any(name => !expectedNames.Contains(name, StringComparer.Ordinal)))
    {
        throw new McpRequestException(-32602, "La richiesta contiene parametri non ammessi.");
    }
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
