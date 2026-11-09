using System.Text.Json;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Protocollo MCP (JSON-RPC su righe) dello strumento di delega. Non conosce il trasporto:
/// riceve una riga e restituisce la risposta; la delega effettiva è una funzione iniettata.
/// </summary>
public sealed class CoordinationMcpServer
{
    /// <summary>Nome del server dichiarato in <c>initialize</c>.</summary>
    public const string ServerName = "benchmark-coordination";

    /// <summary>Versione del protocollo MCP, la stessa del server del workspace.</summary>
    public const string ProtocolVersion = "2025-06-18";

    /// <summary>Unico strumento esposto.</summary>
    public const string ToolName = "delegate_task";

    /// <summary>Unico parametro dello strumento.</summary>
    public const string TaskParameterName = "task";

    /// <summary>
    /// Lunghezza massima del compito in caratteri UTF-16. Limite tecnico contro richieste
    /// patologiche, non un vincolo sperimentale: il budget di token resta il limite effettivo.
    /// </summary>
    public const int MaxTaskLength = 20_000;

    /// <summary>Codice JSON-RPC per un canale di coordinamento non disponibile.</summary>
    public const int ChannelUnavailableErrorCode = -32003;

    private const string ToolDescription =
        "Affida un compito a un lavoratore: un agente separato con gli stessi strumenti del workspace, "
        + "che non vede questa conversazione e riceve solo il testo del compito. "
        + "La chiamata attende la fine del lavoratore e restituisce il suo riepilogo finale. "
        + "È attivo un solo lavoratore alla volta.";

    private const string TaskParameterDescription =
        "Compito completo e autonomo per il lavoratore.";

    private readonly Func<string, CancellationToken, Task<DelegationOutcome>> delegateAsync;

    /// <summary>
    /// Crea il protocollo.
    /// </summary>
    /// <param name="delegateAsync">Inoltra il compito all'orchestratore e ne restituisce l'esito.</param>
    public CoordinationMcpServer(Func<string, CancellationToken, Task<DelegationOutcome>> delegateAsync)
    {
        this.delegateAsync = delegateAsync ?? throw new ArgumentNullException(nameof(delegateAsync));
    }

    /// <summary>
    /// Gestisce una riga ricevuta dal client MCP.
    /// </summary>
    /// <param name="line">Riga JSON-RPC.</param>
    /// <param name="cancellationToken">Annullamento.</param>
    /// <returns>La risposta su una riga, oppure <see langword="null"/> per notifiche e righe vuote.</returns>
    public async Task<string?> HandleLineAsync(string line, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        JsonElement id = default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement request = document.RootElement;
            if (request.ValueKind != JsonValueKind.Object)
            {
                throw new McpProtocolException(-32600, "La richiesta deve essere un oggetto JSON.");
            }

            if (!request.TryGetProperty("id", out JsonElement requestId))
            {
                // Notifica (per esempio notifications/initialized): nessuna risposta.
                return null;
            }

            id = requestId.Clone();
            string? method = request.TryGetProperty("method", out JsonElement methodElement)
                && methodElement.ValueKind == JsonValueKind.String
                ? methodElement.GetString()
                : null;
            object result = method switch
            {
                "initialize" => new
                {
                    protocolVersion = ProtocolVersion,
                    capabilities = new { tools = new { } },
                    serverInfo = new { name = ServerName, version = "1.0.0" },
                },
                "ping" => new { },
                "tools/list" => CreateToolList(),
                "tools/call" => await CallToolAsync(request, cancellationToken),
                _ => throw new McpProtocolException(-32601, "Metodo MCP non supportato."),
            };
            return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result });
        }
        catch (JsonException)
        {
            return SerializeError(default, -32700, "Richiesta JSON non valida.");
        }
        catch (McpProtocolException exception)
        {
            return SerializeError(id, exception.Code, exception.Message);
        }
        catch (CoordinationChannelException exception)
        {
            return SerializeError(id, ChannelUnavailableErrorCode, $"Canale di coordinamento non disponibile: {exception.Message}");
        }
    }

    /// <summary>
    /// Definizione degli strumenti come la riceve il modello (risultato di <c>tools/list</c>).
    /// La descrizione è una condizione sperimentale: il suo hash entra nel record della run.
    /// </summary>
    /// <returns>JSON compatto.</returns>
    public static string DescribeTools() => JsonSerializer.Serialize(CreateToolList());

    private static object CreateToolList() => new
    {
        tools = new[]
        {
            new
            {
                name = ToolName,
                description = ToolDescription,
                inputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        [TaskParameterName] = new
                        {
                            type = "string",
                            description = TaskParameterDescription,
                            minLength = 1,
                            maxLength = MaxTaskLength,
                        },
                    },
                    required = new[] { TaskParameterName },
                    additionalProperties = false,
                },
            },
        },
    };

    private async Task<object> CallToolAsync(JsonElement request, CancellationToken cancellationToken)
    {
        string task = ReadTaskArgument(request);
        DelegationOutcome outcome = await delegateAsync(task, cancellationToken);
        return new
        {
            content = new[] { new { type = "text", text = outcome.Text } },
            isError = outcome.Status != DelegationStatus.Completed,
        };
    }

    /// <summary>
    /// Valida i parametri prima di contattare l'orchestratore: una richiesta non valida
    /// non deve mai avviare un lavoratore.
    /// </summary>
    private static string ReadTaskArgument(JsonElement request)
    {
        if (!request.TryGetProperty("params", out JsonElement parameters) || parameters.ValueKind != JsonValueKind.Object)
        {
            throw new McpProtocolException(-32602, "Parametri mancanti.");
        }

        if (!parameters.TryGetProperty("name", out JsonElement name)
            || name.ValueKind != JsonValueKind.String
            || name.GetString() != ToolName)
        {
            throw new McpProtocolException(-32602, "Strumento sconosciuto.");
        }

        if (!parameters.TryGetProperty("arguments", out JsonElement arguments) || arguments.ValueKind != JsonValueKind.Object)
        {
            throw new McpProtocolException(-32602, "Gli argomenti devono essere un oggetto JSON.");
        }

        string[] names = arguments.EnumerateObject().Select(property => property.Name).ToArray();
        if (names is not [TaskParameterName])
        {
            throw new McpProtocolException(-32602, $"Gli argomenti devono contenere soltanto '{TaskParameterName}'.");
        }

        JsonElement task = arguments.GetProperty(TaskParameterName);
        if (task.ValueKind != JsonValueKind.String)
        {
            throw new McpProtocolException(-32602, $"'{TaskParameterName}' deve essere una stringa.");
        }

        string text = task.GetString()!;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new McpProtocolException(-32602, $"'{TaskParameterName}' non può essere vuoto.");
        }

        if (text.Length > MaxTaskLength)
        {
            throw new McpProtocolException(-32602, $"'{TaskParameterName}' supera {MaxTaskLength} caratteri.");
        }

        return text;
    }

    private static string SerializeError(JsonElement id, int code, string message) => JsonSerializer.Serialize(new
    {
        jsonrpc = "2.0",
        id = id.ValueKind == JsonValueKind.Undefined ? (object?)null : id,
        error = new { code, message },
    });

    private sealed class McpProtocolException(int code, string message) : Exception(message)
    {
        public int Code { get; } = code;
    }
}
