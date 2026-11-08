using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Esito sperimentale di una sessione, con i nomi degli stati di B4.
/// </summary>
public enum SessionStatus
{
    /// <summary>La CLI ha concluso con successo entro i limiti.</summary>
    Completed,

    /// <summary>Superato il limite di tempo.</summary>
    TimeLimitExceeded,

    /// <summary>Superato il budget di token.</summary>
    TokenLimitExceeded,

    /// <summary>Superati tempo e token nello stesso controllo.</summary>
    ResourceLimitsExceeded,

    /// <summary>La configurazione dichiarata dalla CLI non è quella prevista.</summary>
    ConfigurationRejected,

    /// <summary>La CLI è terminata con errore.</summary>
    CliFailed,

    /// <summary>Mancano le metriche necessarie al controllo (B4).</summary>
    UsageMeasurementError,
}

/// <summary>
/// SHA-256 dei materiali e delle configurazioni che determinano le condizioni della sessione.
/// </summary>
/// <param name="TaskPrompt">Prompt del task (specifica pubblica).</param>
/// <param name="CommonInstructions">Istruzione comune alle architetture.</param>
/// <param name="ArchitectureInstructions">Istruzione dell'architettura.</param>
/// <param name="SystemPromptAppend">Testo effettivamente aggiunto al system prompt.</param>
/// <param name="CliSettings">File <c>--settings</c>.</param>
/// <param name="McpConfiguration">File <c>--mcp-config</c>.</param>
/// <param name="McpServerExecutable">Eseguibile del server MCP.</param>
/// <param name="McpServerAssembly">
/// Assembly <c>.dll</c> accanto all'eseguibile, se presente: per un'applicazione .NET
/// l'eseguibile è solo l'apphost e il codice del server è nell'assembly.
/// </param>
public sealed record SessionHashes(
    string TaskPrompt,
    string CommonInstructions,
    string ArchitectureInstructions,
    string SystemPromptAppend,
    string CliSettings,
    string McpConfiguration,
    string McpServerExecutable,
    string? McpServerAssembly);

/// <summary>
/// Documento finale di una sessione, scritto nella directory dei risultati.
/// </summary>
/// <param name="SchemaVersion">Versione del formato.</param>
/// <param name="RunId">Identificatore della run.</param>
/// <param name="TaskId">Task.</param>
/// <param name="Architecture">Architettura (<c>single-agent</c>).</param>
/// <param name="SourceCommit">Commit della baseline.</param>
/// <param name="Status">Esito sperimentale.</param>
/// <param name="StartedAtUtc">Istante di avvio del processo.</param>
/// <param name="EndedAtUtc">Istante di fine dell'architettura.</param>
/// <param name="AgentTaskDurationMs">Tempo dell'architettura misurato con cronometro monotono (B3/B4).</param>
/// <param name="TimeLimitMs">Limite di tempo applicato.</param>
/// <param name="TokenBudget">Budget di <c>totalTokens</c> applicato.</param>
/// <param name="TokensObserved"><c>totalTokens</c> autorevole se disponibile, altrimenti quello dello stream.</param>
/// <param name="TokenOverage"><c>tokensObserved - tokenBudget</c> se il budget è stato superato.</param>
/// <param name="TokenMeasurementComplete"><see langword="true"/> se è disponibile l'uso autorevole del <c>result</c>.</param>
/// <param name="AuthoritativeUsage">Uso del <c>result</c> (B4).</param>
/// <param name="StreamUsage">Somma dell'uso per chiamata ricostruito dallo stream.</param>
/// <param name="StreamOutputFinalForAllMessages">Se tutte le chiamate hanno l'output finale da <c>message_delta</c>.</param>
/// <param name="StreamMatchesAuthoritativeUsage">Confronto tra stream e <c>result</c>, se entrambi presenti.</param>
/// <param name="TerminatedByController">Se il controllore ha terminato il processo.</param>
/// <param name="TerminationReason">Motivo della terminazione, se avvenuta.</param>
/// <param name="ExitCode">Codice di uscita del processo.</param>
/// <param name="RequestedModel">Modello richiesto.</param>
/// <param name="CliVersion">Output di <c>claude --version</c>.</param>
/// <param name="Initialization">Configurazione dichiarata dalla CLI.</param>
/// <param name="ConfigurationViolations">Differenze dalla configurazione attesa.</param>
/// <param name="Result">Dati dell'evento <c>result</c>.</param>
/// <param name="Messages">Uso per chiamata dallo stream.</param>
/// <param name="ToolCalls">Chiamate per strumento.</param>
/// <param name="ToolErrorCount">Risultati di strumento in errore.</param>
/// <param name="StreamLineCount">Righe dello stream.</param>
/// <param name="MalformedLineCount">Righe non interpretabili.</param>
/// <param name="Hashes">Hash delle condizioni della sessione.</param>
/// <param name="ClaudePath">Eseguibile della CLI.</param>
/// <param name="McpServerPath">Eseguibile del server MCP.</param>
/// <param name="RemovedEnvironmentVariables">Nomi delle variabili ereditate rimosse.</param>
/// <param name="ProtectedIntegrityDifferences">Differenze nei materiali protetti a fine sessione.</param>
public sealed record SessionRecord(
    int SchemaVersion,
    string RunId,
    string TaskId,
    string Architecture,
    string SourceCommit,
    SessionStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    long AgentTaskDurationMs,
    long TimeLimitMs,
    long TokenBudget,
    long TokensObserved,
    long? TokenOverage,
    bool TokenMeasurementComplete,
    TokenUsage? AuthoritativeUsage,
    TokenUsage StreamUsage,
    bool StreamOutputFinalForAllMessages,
    bool? StreamMatchesAuthoritativeUsage,
    bool TerminatedByController,
    string? TerminationReason,
    int? ExitCode,
    string RequestedModel,
    string CliVersion,
    SessionInitialization? Initialization,
    IReadOnlyList<string> ConfigurationViolations,
    SessionResult? Result,
    IReadOnlyList<MessageUsage> Messages,
    IReadOnlyDictionary<string, int> ToolCalls,
    int ToolErrorCount,
    int StreamLineCount,
    int MalformedLineCount,
    SessionHashes Hashes,
    string ClaudePath,
    string McpServerPath,
    IReadOnlyList<string> RemovedEnvironmentVariables,
    IReadOnlyList<string> ProtectedIntegrityDifferences)
{
    /// <summary>Versione corrente del formato.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Opzioni di serializzazione: camelCase e stati in kebab-case.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NewLine = "\n",
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    /// <summary>
    /// Scrive il record come JSON UTF-8 senza BOM. Il file non deve già esistere.
    /// </summary>
    /// <param name="path">Percorso assoluto.</param>
    public void WriteTo(string path)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, this, SerializerOptions);
    }
}

/// <summary>
/// Errore che impedisce di avviare o registrare una sessione.
/// </summary>
public sealed class SessionExecutionException : Exception
{
    /// <summary>Crea l'eccezione con un messaggio diagnostico.</summary>
    /// <param name="message">Messaggio diagnostico.</param>
    public SessionExecutionException(string message)
        : base(message)
    {
    }

    /// <summary>Crea l'eccezione con un messaggio diagnostico e la causa.</summary>
    /// <param name="message">Messaggio diagnostico.</param>
    /// <param name="innerException">Causa.</param>
    public SessionExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
