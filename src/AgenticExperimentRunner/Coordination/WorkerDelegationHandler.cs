using AgenticExperimentRunner.SessionExecution;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Esegue le deleghe avviando ogni lavoratore come sessione CLI separata sotto lo stesso
/// controllore dei limiti della run, e registra eventi ed esiti per il record (B5).
/// </summary>
/// <remarks>
/// L'host garantisce una sola delega alla volta, quindi lo stato interno non è condiviso
/// tra chiamate concorrenti; viene letto solo dopo la chiusura dell'host.
/// </remarks>
public sealed class WorkerDelegationHandler : IDelegationHandler
{
    /// <summary>Motivo registrato per un lavoratore terminato perché il coordinatore ha concluso.</summary>
    public const string CoordinatorEndedReason = "il coordinatore ha concluso prima della fine del lavoratore";

    private readonly Func<string, string, string, AgentLaunch> createWorkerLaunch;
    private readonly Func<int, string> selectAgentId;
    private readonly Func<int, string> selectRole;
    private readonly RunLimitController limits;
    private readonly TimeProvider timeProvider;
    private readonly int maxDelegations;
    private readonly CancellationToken runCancellation;
    private readonly List<DelegationEvent> events = [];
    private readonly List<(AgentProcessOutcome Outcome, long RequestId)> workers = [];

    /// <summary>
    /// Crea il gestore.
    /// </summary>
    /// <param name="createWorkerLaunch">
    /// Prepara l'avvio di un lavoratore dati identificatore e compito (crea la directory
    /// dell'agente e scrive il prompt).
    /// </param>
    /// <param name="limits">Controllore della run, già avviato prima della prima delega.</param>
    /// <param name="timeProvider">Orologio degli istanti registrati.</param>
    /// <param name="maxDelegations">Tetto tecnico delle richieste di delega della run.</param>
    /// <param name="runCancellation">Annullamento esterno dell'intera run.</param>
    public WorkerDelegationHandler(
        Func<string, string, AgentLaunch> createWorkerLaunch,
        RunLimitController limits,
        TimeProvider timeProvider,
        int maxDelegations,
        CancellationToken runCancellation)
        : this(
            WrapLegacyFactory(createWorkerLaunch),
            sequence => $"worker-{sequence:00}",
            _ => HierarchicalRunRunner.WorkerRole,
            limits,
            timeProvider,
            maxDelegations,
            runCancellation)
    {
    }

    private static Func<string, string, string, AgentLaunch> WrapLegacyFactory(
        Func<string, string, AgentLaunch> createWorkerLaunch)
    {
        ArgumentNullException.ThrowIfNull(createWorkerLaunch);
        return (agentId, _, task) => createWorkerLaunch(agentId, task);
    }

    /// <summary>
    /// Crea un gestore con identificatori e ruoli determinati meccanicamente dal numero di sequenza.
    /// </summary>
    /// <param name="createWorkerLaunch">Prepara l'avvio usando identificatore, ruolo e compito.</param>
    /// <param name="selectAgentId">Restituisce l'identificatore per una sequenza a base uno.</param>
    /// <param name="selectRole">Restituisce il ruolo per una sequenza a base uno.</param>
    /// <param name="limits">Controllore globale della run.</param>
    /// <param name="timeProvider">Orologio degli eventi.</param>
    /// <param name="maxDelegations">Numero massimo di richieste.</param>
    /// <param name="runCancellation">Annullamento esterno della run.</param>
    public WorkerDelegationHandler(
        Func<string, string, string, AgentLaunch> createWorkerLaunch,
        Func<int, string> selectAgentId,
        Func<int, string> selectRole,
        RunLimitController limits,
        TimeProvider timeProvider,
        int maxDelegations,
        CancellationToken runCancellation)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDelegations);
        this.createWorkerLaunch = createWorkerLaunch ?? throw new ArgumentNullException(nameof(createWorkerLaunch));
        this.selectAgentId = selectAgentId ?? throw new ArgumentNullException(nameof(selectAgentId));
        this.selectRole = selectRole ?? throw new ArgumentNullException(nameof(selectRole));
        this.limits = limits ?? throw new ArgumentNullException(nameof(limits));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.maxDelegations = maxDelegations;
        this.runCancellation = runCancellation;
    }

    /// <summary>Eventi di delega in ordine di arrivo.</summary>
    public IReadOnlyList<DelegationEvent> Events => events;

    /// <summary>Esiti dei lavoratori avviati, con la delega che li ha avviati.</summary>
    public IReadOnlyList<(AgentProcessOutcome Outcome, long RequestId)> Workers => workers;

    /// <inheritdoc/>
    /// <remarks>
    /// <paramref name="cancellationToken"/> è annullato dall'host quando il coordinatore ha
    /// concluso: il lavoratore in corso viene terminato e registrato come fallito.
    /// </remarks>
    public async Task<DelegationOutcome> DelegateAsync(DelegationRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset requestedAt = timeProvider.GetUtcNow();
        int sequence = events.Count + 1;
        string taskHash = RunInputs.Sha256(request.Task);

        string? rejection = sequence > maxDelegations
            ? $"raggiunto il numero massimo di {maxDelegations} deleghe per questa run"
            : limits.StopReason is { } stopReason
                ? $"la run si sta fermando ({stopReason})"
                : null;
        if (rejection is not null)
        {
            events.Add(new DelegationEvent(
                request.RequestId, sequence, DelegationStatus.Rejected, null, rejection,
                requestedAt, null, timeProvider.GetUtcNow(), taskHash, request.Task.Length));
            return new DelegationOutcome(
                DelegationStatus.Rejected,
                $"Delega rifiutata: {rejection}. Nessun lavoratore è stato avviato.");
        }

        string agentId = selectAgentId(sequence);
        string role = selectRole(sequence);
        if (string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(role))
        {
            throw new InvalidOperationException($"Identificatore e ruolo devono essere valorizzati per la delega {sequence}.");
        }

        AgentLaunch launch = createWorkerLaunch(agentId, role, request.Task);
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        AgentProcessOutcome outcome = await AgentProcess.RunAsync(
            launch, limits, timeProvider, cancellationToken, CoordinatorEndedReason, runCancellation);
        workers.Add((outcome, request.RequestId));

        (DelegationStatus status, string? reason, string text) = Describe(outcome);
        events.Add(new DelegationEvent(
            request.RequestId, sequence, status, agentId, reason,
            requestedAt, startedAt, timeProvider.GetUtcNow(), taskHash, request.Task.Length));
        return new DelegationOutcome(status, text);
    }

    private static (DelegationStatus Status, string? Reason, string Text) Describe(AgentProcessOutcome outcome)
    {
        string? summary = string.IsNullOrWhiteSpace(outcome.Tracker.Result?.ResultText)
            ? null
            : outcome.Tracker.Result!.ResultText;
        if (outcome.Succeeded)
        {
            return (DelegationStatus.Completed, null, summary ?? "Il lavoratore ha concluso senza riepilogo.");
        }

        string reason = outcome.TerminationReason
            ?? (outcome.Tracker.Result is { } result
                ? $"esito '{result.Subtype}' con codice di uscita {outcome.ExitCode}"
                : $"sessione terminata con codice {outcome.ExitCode} senza esito");
        string text = $"Il lavoratore non ha completato il compito: {reason}.";
        return (DelegationStatus.Failed, reason, summary is null ? text : $"{text}\n\nUltimo riepilogo del lavoratore:\n{summary}");
    }
}
