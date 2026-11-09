using System.Text.Json;
using AgenticExperimentRunner.SessionExecution;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Dati di un agente della run (coordinatore o lavoratore), ricavati dal suo processo.
/// </summary>
/// <param name="AgentId">Identificatore nella run.</param>
/// <param name="Role">Ruolo (<c>coordinator</c> o <c>worker</c>).</param>
/// <param name="DelegationRequestId">Delega che ha avviato il lavoratore; assente per il coordinatore.</param>
/// <param name="Directory">Directory dei file dell'agente, relativa alla directory dei risultati.</param>
/// <param name="StartedAtUtc">Avvio del processo.</param>
/// <param name="EndedAtUtc">Uscita del processo.</param>
/// <param name="DurationMs">Durata misurata con cronometro monotono.</param>
/// <param name="ExitCode">Codice di uscita.</param>
/// <param name="TerminatedByController">Se il processo è stato terminato dall'orchestratore.</param>
/// <param name="TerminationReason">Motivo della terminazione.</param>
/// <param name="TokenMeasurementComplete">Se è disponibile l'uso autorevole del <c>result</c>.</param>
/// <param name="AuthoritativeUsage">Uso del <c>result</c>, se presente.</param>
/// <param name="StreamUsage">Uso per chiamata sommato dallo stream.</param>
/// <param name="StreamOutputFinalForAllMessages">Se tutte le chiamate hanno l'output finale da <c>message_delta</c>.</param>
/// <param name="StreamMatchesAuthoritativeUsage">Confronto tra stream e <c>result</c>, se entrambi presenti.</param>
/// <param name="Initialization">Configurazione dichiarata dalla CLI.</param>
/// <param name="ConfigurationViolations">Differenze dalla configurazione attesa.</param>
/// <param name="Result">Dati dell'evento <c>result</c>.</param>
/// <param name="Messages">Uso per chiamata.</param>
/// <param name="ToolCalls">Chiamate per strumento.</param>
/// <param name="ToolErrorCount">Risultati di strumento in errore.</param>
/// <param name="StreamLineCount">Righe dello stream.</param>
/// <param name="MalformedLineCount">Righe non interpretabili.</param>
/// <param name="RemovedEnvironmentVariables">Nomi delle variabili ereditate rimosse.</param>
public sealed record AgentRecord(
    string AgentId,
    string Role,
    long? DelegationRequestId,
    string Directory,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    long DurationMs,
    int ExitCode,
    bool TerminatedByController,
    string? TerminationReason,
    bool TokenMeasurementComplete,
    TokenUsage? AuthoritativeUsage,
    TokenUsage StreamUsage,
    bool StreamOutputFinalForAllMessages,
    bool? StreamMatchesAuthoritativeUsage,
    SessionInitialization? Initialization,
    IReadOnlyList<string> ConfigurationViolations,
    SessionResult? Result,
    IReadOnlyList<MessageUsage> Messages,
    IReadOnlyDictionary<string, int> ToolCalls,
    int ToolErrorCount,
    int StreamLineCount,
    int MalformedLineCount,
    IReadOnlyList<string> RemovedEnvironmentVariables)
{
    /// <summary>Uso contabilizzato: quello autorevole se presente, altrimenti quello dello stream.</summary>
    public TokenUsage CountedUsage => AuthoritativeUsage ?? StreamUsage;

    /// <summary>
    /// Costruisce il record dall'esito del processo.
    /// </summary>
    /// <param name="outcome">Esito del processo.</param>
    /// <param name="delegationRequestId">Delega che ha avviato l'agente, se lavoratore.</param>
    /// <param name="directory">Directory dei file, relativa alla directory dei risultati.</param>
    /// <returns>Il record.</returns>
    public static AgentRecord FromOutcome(AgentProcessOutcome outcome, long? delegationRequestId, string directory)
    {
        StreamUsageTracker tracker = outcome.Tracker;
        TokenUsage? authoritative = tracker.Result?.Usage;
        return new AgentRecord(
            outcome.AgentId,
            outcome.Role,
            delegationRequestId,
            directory,
            outcome.StartedAt,
            outcome.EndedAt,
            (long)outcome.Duration.TotalMilliseconds,
            outcome.ExitCode,
            outcome.TerminatedByController,
            outcome.TerminationReason,
            authoritative is not null,
            authoritative,
            tracker.ObservedUsage,
            tracker.Messages.Count > 0 && tracker.Messages.All(message => message.OutputIsFinal),
            authoritative is null ? null : authoritative == tracker.ObservedUsage,
            tracker.Initialization,
            outcome.ConfigurationViolations,
            tracker.Result,
            tracker.Messages,
            tracker.ToolCalls,
            tracker.ToolErrorCount,
            tracker.LineCount,
            tracker.MalformedLineCount,
            outcome.RemovedEnvironmentVariables);
    }
}

/// <summary>
/// Evento di delega (B5): una richiesta del coordinatore e il suo esito.
/// </summary>
/// <param name="RequestId">Identificatore assegnato dal relay.</param>
/// <param name="Sequence">Posizione della richiesta nella run, da 1.</param>
/// <param name="Status">Esito.</param>
/// <param name="AgentId">Lavoratore avviato; assente se la delega è stata rifiutata.</param>
/// <param name="Reason">Motivo del rifiuto o del fallimento.</param>
/// <param name="RequestedAtUtc">Ricezione della richiesta.</param>
/// <param name="StartedAtUtc">Avvio del lavoratore.</param>
/// <param name="EndedAtUtc">Risposta al coordinatore.</param>
/// <param name="TaskSha256">Hash del compito; il testo è nel <c>prompt.md</c> del lavoratore.</param>
/// <param name="TaskLength">Lunghezza del compito in caratteri.</param>
public sealed record DelegationEvent(
    long RequestId,
    int Sequence,
    DelegationStatus Status,
    string? AgentId,
    string? Reason,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    string TaskSha256,
    int TaskLength);

/// <summary>
/// Conteggi delle deleghe della run (B5).
/// </summary>
/// <param name="Requested">Richieste ricevute.</param>
/// <param name="Started">Lavoratori avviati.</param>
/// <param name="Completed">Deleghe completate.</param>
/// <param name="Failed">Lavoratori avviati che non hanno completato.</param>
/// <param name="Rejected">Richieste rifiutate senza avviare un lavoratore.</param>
public sealed record DelegationCounts(int Requested, int Started, int Completed, int Failed, int Rejected)
{
    /// <summary>
    /// Conta gli eventi per esito.
    /// </summary>
    /// <param name="events">Eventi di delega.</param>
    /// <returns>I conteggi.</returns>
    public static DelegationCounts From(IReadOnlyCollection<DelegationEvent> events) => new(
        events.Count,
        events.Count(item => item.AgentId is not null),
        events.Count(item => item.Status == DelegationStatus.Completed),
        events.Count(item => item.Status == DelegationStatus.Failed),
        events.Count(item => item.Status == DelegationStatus.Rejected));
}

/// <summary>
/// SHA-256 delle condizioni della run gerarchica.
/// </summary>
/// <param name="TaskPrompt">Prompt del coordinatore (specifica pubblica).</param>
/// <param name="CommonInstructions">Istruzione comune alle architetture.</param>
/// <param name="CoordinatorInstructions">Istruzione del coordinatore.</param>
/// <param name="WorkerInstructions">Istruzione del lavoratore.</param>
/// <param name="CoordinatorSystemPromptAppend">Testo aggiunto al system prompt del coordinatore.</param>
/// <param name="WorkerSystemPromptAppend">Testo aggiunto al system prompt dei lavoratori.</param>
/// <param name="CliSettings">File <c>--settings</c>, comune a tutti gli agenti.</param>
/// <param name="CoordinatorMcpConfiguration">Configurazione MCP del coordinatore.</param>
/// <param name="WorkerMcpConfiguration">Configurazione MCP dei lavoratori.</param>
/// <param name="DelegateToolDefinition">Definizione di <c>delegate_task</c> vista dal modello.</param>
/// <param name="McpServerExecutable">Eseguibile del server MCP del workspace.</param>
/// <param name="McpServerAssembly">Assembly del server MCP del workspace.</param>
/// <param name="CoordinationServerExecutable">Eseguibile del relay di coordinamento.</param>
/// <param name="CoordinationServerAssembly">Assembly del relay di coordinamento.</param>
public sealed record HierarchyHashes(
    string TaskPrompt,
    string CommonInstructions,
    string CoordinatorInstructions,
    string WorkerInstructions,
    string CoordinatorSystemPromptAppend,
    string WorkerSystemPromptAppend,
    string CliSettings,
    string CoordinatorMcpConfiguration,
    string WorkerMcpConfiguration,
    string DelegateToolDefinition,
    string McpServerExecutable,
    string? McpServerAssembly,
    string CoordinationServerExecutable,
    string? CoordinationServerAssembly);

/// <summary>
/// Documento finale di una run gerarchica, scritto nella directory dei risultati.
/// </summary>
/// <param name="SchemaVersion">Versione del formato.</param>
/// <param name="RunId">Identificatore della run.</param>
/// <param name="TaskId">Task.</param>
/// <param name="Architecture">Architettura.</param>
/// <param name="SourceCommit">Commit della baseline.</param>
/// <param name="Status">Esito sperimentale della run.</param>
/// <param name="StartedAtUtc">Avvio del controllore, immediatamente prima del coordinatore.</param>
/// <param name="EndedAtUtc">Fine della run.</param>
/// <param name="AgentTaskDurationMs">Tempo dell'architettura misurato con cronometro monotono (B3/B4).</param>
/// <param name="TimeLimitMs">Limite di tempo applicato.</param>
/// <param name="TokenBudget">Budget globale di <c>totalTokens</c>.</param>
/// <param name="MaxDelegations">Tetto tecnico delle deleghe.</param>
/// <param name="TokensObserved">Somma dell'uso contabilizzato di tutti gli agenti.</param>
/// <param name="TokenOverage"><c>tokensObserved - tokenBudget</c> se il budget è stato superato.</param>
/// <param name="TokenMeasurementComplete">Se tutti gli agenti hanno l'uso autorevole.</param>
/// <param name="TotalUsage">Somma delle quattro categorie su tutti gli agenti.</param>
/// <param name="UsageByRole">Subtotali per ruolo, diagnostici (B5).</param>
/// <param name="StopReason">Prima causa della fermata della run, se avvenuta.</param>
/// <param name="RequestedModel">Modello richiesto per tutti gli agenti.</param>
/// <param name="CliVersion">Output di <c>claude --version</c>.</param>
/// <param name="Agents">Agenti nell'ordine di avvio, coordinatore per primo.</param>
/// <param name="Delegations">Eventi di delega in ordine.</param>
/// <param name="DelegationCounts">Conteggi delle deleghe.</param>
/// <param name="MaxConcurrentWorkers">Massimo di lavoratori attivi insieme (atteso 1).</param>
/// <param name="Hashes">Hash delle condizioni.</param>
/// <param name="ClaudePath">Eseguibile della CLI.</param>
/// <param name="McpServerPath">Eseguibile del server MCP del workspace.</param>
/// <param name="CoordinationServerPath">Eseguibile del relay di coordinamento.</param>
/// <param name="ProtectedIntegrityDifferences">Differenze nei materiali protetti a fine run.</param>
/// <param name="Toolchain">Identità della toolchain dei comandi MCP di sviluppo.</param>
public sealed record HierarchicalRunRecord(
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
    int MaxDelegations,
    long TokensObserved,
    long? TokenOverage,
    bool TokenMeasurementComplete,
    TokenUsage TotalUsage,
    IReadOnlyDictionary<string, TokenUsage> UsageByRole,
    string? StopReason,
    string RequestedModel,
    string CliVersion,
    IReadOnlyList<AgentRecord> Agents,
    IReadOnlyList<DelegationEvent> Delegations,
    DelegationCounts DelegationCounts,
    int MaxConcurrentWorkers,
    HierarchyHashes Hashes,
    string ClaudePath,
    string McpServerPath,
    string CoordinationServerPath,
    IReadOnlyList<string> ProtectedIntegrityDifferences,
    ToolchainIdentity Toolchain)
{
    /// <summary>Versione corrente del formato.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Scrive il record come JSON UTF-8 senza BOM. Il file non deve già esistere.
    /// </summary>
    /// <param name="path">Percorso assoluto.</param>
    public void WriteTo(string path)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, this, SessionRecord.SerializerOptions);
    }
}
