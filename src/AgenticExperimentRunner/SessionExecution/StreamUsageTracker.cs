using System.Text.Json;

namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Le quattro categorie di token di B4.
/// </summary>
/// <param name="InputTokens">Token di input non serviti dalla cache.</param>
/// <param name="CacheReadInputTokens">Token di input letti dalla cache.</param>
/// <param name="CacheCreationInputTokens">Token di input usati per creare la cache.</param>
/// <param name="OutputTokens">Token di output.</param>
public sealed record TokenUsage(
    long InputTokens,
    long CacheReadInputTokens,
    long CacheCreationInputTokens,
    long OutputTokens)
{
    /// <summary>Uso nullo.</summary>
    public static readonly TokenUsage Zero = new(0, 0, 0, 0);

    /// <summary><c>totalInput</c> di B4: input più cache read e cache creation.</summary>
    public long TotalInputTokens => InputTokens + CacheReadInputTokens + CacheCreationInputTokens;

    /// <summary><c>totalTokens</c> di B4: grandezza su cui si applica il budget.</summary>
    public long TotalTokens => TotalInputTokens + OutputTokens;

    /// <summary>Somma per categoria.</summary>
    /// <param name="other">Uso da aggiungere.</param>
    /// <returns>La somma.</returns>
    public TokenUsage Add(TokenUsage other) => new(
        InputTokens + other.InputTokens,
        CacheReadInputTokens + other.CacheReadInputTokens,
        CacheCreationInputTokens + other.CacheCreationInputTokens,
        OutputTokens + other.OutputTokens);

    /// <summary>Massimo per categoria, per eventi ripetuti dello stesso messaggio.</summary>
    /// <param name="other">Altro valore osservato.</param>
    /// <returns>Il massimo.</returns>
    public TokenUsage Max(TokenUsage other) => new(
        Math.Max(InputTokens, other.InputTokens),
        Math.Max(CacheReadInputTokens, other.CacheReadInputTokens),
        Math.Max(CacheCreationInputTokens, other.CacheCreationInputTokens),
        Math.Max(OutputTokens, other.OutputTokens));
}

/// <summary>
/// Uso di una singola chiamata al modello, ricostruito dallo stream.
/// </summary>
/// <param name="MessageId">Identificatore del messaggio restituito dal modello.</param>
/// <param name="Model">Modello che ha prodotto il messaggio.</param>
/// <param name="Usage">Uso osservato.</param>
/// <param name="OutputIsFinal">
/// <see langword="true"/> se l'output proviene da <c>message_delta</c> (valore finale);
/// altrimenti è il massimo visto negli eventi <c>assistant</c>, che può essere parziale.
/// </param>
public sealed record MessageUsage(string MessageId, string? Model, TokenUsage Usage, bool OutputIsFinal);

/// <summary>
/// Stato di un server MCP dichiarato all'avvio della sessione.
/// </summary>
/// <param name="Name">Nome del server.</param>
/// <param name="Status">Stato riportato (per esempio <c>connected</c>).</param>
public sealed record McpServerState(string Name, string Status);

/// <summary>
/// Configurazione effettiva dichiarata dalla CLI nell'evento <c>system/init</c>.
/// </summary>
/// <param name="Model">Modello della sessione.</param>
/// <param name="CliVersion">Versione della CLI.</param>
/// <param name="SessionId">Identificatore della sessione.</param>
/// <param name="Tools">Strumenti invocabili dal modello.</param>
/// <param name="McpServers">Server MCP e relativo stato.</param>
/// <param name="Agents">Tipi di subagente disponibili.</param>
/// <param name="Skills">Skill disponibili.</param>
/// <param name="SlashCommands">Comandi slash disponibili.</param>
/// <param name="PermissionMode">Modalità dei permessi.</param>
public sealed record SessionInitialization(
    string? Model,
    string? CliVersion,
    string? SessionId,
    IReadOnlyList<string> Tools,
    IReadOnlyList<McpServerState> McpServers,
    IReadOnlyList<string> Agents,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> SlashCommands,
    string? PermissionMode);

/// <summary>
/// Dati dell'evento finale <c>result</c>.
/// </summary>
/// <param name="Subtype">Esito riportato (per esempio <c>success</c>).</param>
/// <param name="IsError">Indicatore di errore riportato dalla CLI.</param>
/// <param name="TerminalReason">Motivo di chiusura riportato.</param>
/// <param name="NumTurns">Numero di turni riportato.</param>
/// <param name="DurationMs">Durata riportata dalla CLI, in millisecondi.</param>
/// <param name="ReportedCostUsd">Costo riportato dalla CLI; non è la stima di B4.</param>
/// <param name="Usage">Uso complessivo autorevole (B4), se presente.</param>
/// <param name="Iterations">Uso per chiamata riportato nel campo <c>usage.iterations</c>.</param>
/// <param name="Models">Modelli elencati in <c>modelUsage</c>.</param>
/// <param name="PermissionDenialCount">Numero di permessi negati.</param>
/// <param name="ResultText">Testo finale della sessione (campo <c>result</c>), se presente.</param>
public sealed record SessionResult(
    string? Subtype,
    bool? IsError,
    string? TerminalReason,
    long? NumTurns,
    long? DurationMs,
    decimal? ReportedCostUsd,
    TokenUsage? Usage,
    IReadOnlyList<TokenUsage> Iterations,
    IReadOnlyList<string> Models,
    int PermissionDenialCount,
    string? ResultText);

/// <summary>
/// Tipo di riga osservata, per le decisioni del controllore.
/// </summary>
public enum StreamLineKind
{
    /// <summary>Evento <c>system/init</c>.</summary>
    Initialization,

    /// <summary>Evento che aggiorna l'uso dei token.</summary>
    Usage,

    /// <summary>Evento finale <c>result</c>.</summary>
    Result,

    /// <summary>Altro evento valido.</summary>
    Other,

    /// <summary>Riga vuota o non interpretabile come oggetto JSON.</summary>
    Malformed,
}

/// <summary>
/// Interpreta, una riga alla volta, lo stream <c>stream-json</c> della CLI e mantiene
/// configurazione, uso dei token e attività degli strumenti. Non accede a processi o file.
/// </summary>
/// <remarks>
/// Regole verificate sullo stream della prova P7: più eventi <c>assistant</c> con lo
/// stesso <c>message.id</c> ripetono lo stesso uso e si contano una volta; input e
/// cache per messaggio coincidono con il <c>result</c>, l'output no. Con
/// <c>--include-partial-messages</c> l'evento <c>message_delta</c> porta l'output del
/// messaggio aperto dall'ultimo <c>message_start</c>.
/// </remarks>
public sealed class StreamUsageTracker
{
    private readonly Dictionary<string, MessageUsage> messages = new(StringComparer.Ordinal);
    private readonly List<string> messageOrder = [];
    private readonly Dictionary<string, int> toolCalls = new(StringComparer.Ordinal);
    private readonly HashSet<string> toolUseIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> failedToolUseIds = new(StringComparer.Ordinal);
    private string? openStreamMessageId;

    /// <summary>Configurazione dichiarata al primo <c>system/init</c>.</summary>
    public SessionInitialization? Initialization { get; private set; }

    /// <summary>Ultimo evento <c>result</c>.</summary>
    public SessionResult? Result { get; private set; }

    /// <summary>Righe ricevute, incluse quelle non valide.</summary>
    public int LineCount { get; private set; }

    /// <summary>Righe non interpretabili come oggetto JSON.</summary>
    public int MalformedLineCount { get; private set; }

    /// <summary>Uso per chiamata, nell'ordine di prima osservazione.</summary>
    public IReadOnlyList<MessageUsage> Messages => messageOrder.Select(id => messages[id]).ToList();

    /// <summary>Numero di chiamate distinte a ciascuno strumento.</summary>
    public IReadOnlyDictionary<string, int> ToolCalls => toolCalls;

    /// <summary>Numero di risultati di strumento marcati come errore.</summary>
    public int ToolErrorCount => failedToolUseIds.Count;

    /// <summary>Somma dell'uso per chiamata osservato finora.</summary>
    public TokenUsage ObservedUsage =>
        messages.Values.Aggregate(TokenUsage.Zero, (total, message) => total.Add(message.Usage));

    /// <summary>
    /// Interpreta una riga dello stream.
    /// </summary>
    /// <param name="line">Riga ricevuta dallo stdout della CLI.</param>
    /// <returns>Il tipo di riga.</returns>
    public StreamLineKind Observe(string line)
    {
        LineCount++;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            MalformedLineCount++;
            return StreamLineKind.Malformed;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                MalformedLineCount++;
                return StreamLineKind.Malformed;
            }

            return GetString(root, "type") switch
            {
                "system" when GetString(root, "subtype") == "init" => ObserveInitialization(root),
                "assistant" => ObserveAssistant(root),
                "user" => ObserveUser(root),
                "stream_event" => ObserveStreamEvent(root),
                "result" => ObserveResult(root),
                _ => StreamLineKind.Other,
            };
        }
    }

    private StreamLineKind ObserveInitialization(JsonElement root)
    {
        // Solo il primo init descrive la configurazione con cui la sessione è partita.
        Initialization ??= new SessionInitialization(
            GetString(root, "model"),
            GetString(root, "claude_code_version"),
            GetString(root, "session_id"),
            GetStrings(root, "tools"),
            GetArray(root, "mcp_servers")
                .Select(server => new McpServerState(GetString(server, "name") ?? "", GetString(server, "status") ?? ""))
                .ToList(),
            GetStrings(root, "agents"),
            GetStrings(root, "skills"),
            GetStrings(root, "slash_commands"),
            GetString(root, "permissionMode"));
        return StreamLineKind.Initialization;
    }

    private StreamLineKind ObserveAssistant(JsonElement root)
    {
        if (!root.TryGetProperty("message", out JsonElement message) || message.ValueKind != JsonValueKind.Object)
        {
            return StreamLineKind.Other;
        }

        foreach (JsonElement block in GetArray(message, "content"))
        {
            if (GetString(block, "type") == "tool_use"
                && GetString(block, "id") is { } toolUseId
                && toolUseIds.Add(toolUseId))
            {
                string name = GetString(block, "name") ?? "";
                toolCalls[name] = toolCalls.GetValueOrDefault(name) + 1;
            }
        }

        string? messageId = GetString(message, "id");
        if (messageId is null || !message.TryGetProperty("usage", out JsonElement usage))
        {
            return StreamLineKind.Other;
        }

        RecordUsage(messageId, GetString(message, "model"), ReadUsage(usage), outputIsFinal: false);
        return StreamLineKind.Usage;
    }

    private StreamLineKind ObserveUser(JsonElement root)
    {
        if (root.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonElement block in GetArray(message, "content"))
            {
                if (GetString(block, "type") == "tool_result"
                    && block.TryGetProperty("is_error", out JsonElement isError)
                    && isError.ValueKind == JsonValueKind.True
                    && GetString(block, "tool_use_id") is { } toolUseId)
                {
                    failedToolUseIds.Add(toolUseId);
                }
            }
        }

        return StreamLineKind.Other;
    }

    private StreamLineKind ObserveStreamEvent(JsonElement root)
    {
        if (!root.TryGetProperty("event", out JsonElement streamEvent) || streamEvent.ValueKind != JsonValueKind.Object)
        {
            return StreamLineKind.Other;
        }

        switch (GetString(streamEvent, "type"))
        {
            case "message_start" when streamEvent.TryGetProperty("message", out JsonElement message):
                openStreamMessageId = GetString(message, "id");
                if (openStreamMessageId is not null && message.TryGetProperty("usage", out JsonElement startUsage))
                {
                    RecordUsage(openStreamMessageId, GetString(message, "model"), ReadUsage(startUsage), outputIsFinal: false);
                    return StreamLineKind.Usage;
                }

                return StreamLineKind.Other;
            case "message_delta" when openStreamMessageId is not null
                && streamEvent.TryGetProperty("usage", out JsonElement deltaUsage):
                // L'output di message_delta è il valore finale del messaggio.
                RecordUsage(openStreamMessageId, null, ReadUsage(deltaUsage), outputIsFinal: true);
                return StreamLineKind.Usage;
            default:
                return StreamLineKind.Other;
        }
    }

    private StreamLineKind ObserveResult(JsonElement root)
    {
        TokenUsage? usage = null;
        IReadOnlyList<TokenUsage> iterations = [];
        if (root.TryGetProperty("usage", out JsonElement usageElement) && usageElement.ValueKind == JsonValueKind.Object)
        {
            usage = ReadUsage(usageElement);
            iterations = GetArray(usageElement, "iterations").Select(ReadUsage).ToList();
        }

        IReadOnlyList<string> models = root.TryGetProperty("modelUsage", out JsonElement modelUsage)
            && modelUsage.ValueKind == JsonValueKind.Object
                ? modelUsage.EnumerateObject().Select(property => property.Name).ToList()
                : [];

        Result = new SessionResult(
            GetString(root, "subtype"),
            root.TryGetProperty("is_error", out JsonElement isError) && isError.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? isError.GetBoolean()
                : null,
            GetString(root, "terminal_reason"),
            GetInt64(root, "num_turns"),
            GetInt64(root, "duration_ms"),
            root.TryGetProperty("total_cost_usd", out JsonElement cost) && cost.ValueKind == JsonValueKind.Number
                ? cost.GetDecimal()
                : null,
            usage,
            iterations,
            models,
            GetArray(root, "permission_denials").Count(),
            GetString(root, "result"));
        return StreamLineKind.Result;
    }

    private void RecordUsage(string messageId, string? model, TokenUsage observed, bool outputIsFinal)
    {
        if (!messages.TryGetValue(messageId, out MessageUsage? existing))
        {
            messages[messageId] = new MessageUsage(messageId, model, observed, outputIsFinal);
            messageOrder.Add(messageId);
            return;
        }

        TokenUsage merged = existing.Usage.Max(observed);
        if (outputIsFinal)
        {
            merged = merged with { OutputTokens = observed.OutputTokens };
        }
        else if (existing.OutputIsFinal)
        {
            merged = merged with { OutputTokens = existing.Usage.OutputTokens };
        }

        messages[messageId] = existing with
        {
            Model = existing.Model ?? model,
            Usage = merged,
            OutputIsFinal = existing.OutputIsFinal || outputIsFinal,
        };
    }

    private static TokenUsage ReadUsage(JsonElement usage) => new(
        GetInt64(usage, "input_tokens") ?? 0,
        GetInt64(usage, "cache_read_input_tokens") ?? 0,
        GetInt64(usage, "cache_creation_input_tokens") ?? 0,
        GetInt64(usage, "output_tokens") ?? 0);

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? GetInt64(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out long number)
            ? number
            : null;

    private static IEnumerable<JsonElement> GetArray(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => item.Clone()).ToList()
            : [];

    private static IReadOnlyList<string> GetStrings(JsonElement element, string name) =>
        GetArray(element, name)
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToList();
}
