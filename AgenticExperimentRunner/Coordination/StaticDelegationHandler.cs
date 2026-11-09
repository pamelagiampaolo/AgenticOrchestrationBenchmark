using AgenticExperimentRunner.SessionExecution;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Applica il piano statico alle richieste del coordinatore e impedisce salti,
/// deleghe aggiuntive o prosecuzione dopo un fallimento.
/// </summary>
public sealed class StaticDelegationHandler : IDelegationHandler
{
    private readonly WorkerDelegationHandler inner;
    private readonly List<StaticWorkerOutcome> outcomes = [];
    private bool stopped;

    /// <summary>Crea la policy sopra il gestore che avvia materialmente i processi.</summary>
    /// <param name="inner">Gestore configurato con identificatori e ruoli del piano statico.</param>
    public StaticDelegationHandler(WorkerDelegationHandler inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>
    /// Crea la policy e il gestore processi con identificatori e ruoli ricavati dal piano fisso.
    /// </summary>
    /// <param name="createWorkerLaunch">Prepara l'avvio usando identificatore, ruolo e compito.</param>
    /// <param name="limits">Controllore globale già condiviso con il coordinatore.</param>
    /// <param name="timeProvider">Orologio degli eventi.</param>
    /// <param name="runCancellation">Annullamento esterno dell'intera run.</param>
    public StaticDelegationHandler(
        Func<string, string, string, AgentLaunch> createWorkerLaunch,
        RunLimitController limits,
        TimeProvider timeProvider,
        CancellationToken runCancellation)
        : this(new WorkerDelegationHandler(
            createWorkerLaunch,
            sequence => StaticHierarchyPlan.Steps[sequence - 1].AgentId,
            sequence => StaticHierarchyPlan.Steps[sequence - 1].Role,
            limits,
            timeProvider,
            StaticHierarchyPlan.Steps.Count,
            runCancellation))
    {
    }

    /// <summary>Passaggi completati o falliti, nell'ordine fisso.</summary>
    public IReadOnlyList<StaticWorkerOutcome> Outcomes => outcomes;

    /// <summary>Vero soltanto dopo il successo di tutti e tre i ruoli.</summary>
    public bool IsComplete => outcomes.Count == StaticHierarchyPlan.Steps.Count
        && outcomes.All(outcome => outcome.Result.Succeeded);

    /// <summary>Eventi prodotti dal gestore processi condiviso.</summary>
    public IReadOnlyList<DelegationEvent> Events => inner.Events;

    /// <summary>Processi dei lavoratori avviati, associati alle richieste.</summary>
    public IReadOnlyList<(AgentProcessOutcome Outcome, long RequestId)> Workers => inner.Workers;

    /// <inheritdoc/>
    public async Task<DelegationOutcome> DelegateAsync(
        DelegationRequest request,
        CancellationToken cancellationToken)
    {
        if (stopped)
        {
            return new DelegationOutcome(
                DelegationStatus.Rejected,
                "Delega rifiutata: il piano statico è già terminato o fallito.");
        }

        int index = outcomes.Count;
        if (index >= StaticHierarchyPlan.Steps.Count)
        {
            stopped = true;
            return new DelegationOutcome(
                DelegationStatus.Rejected,
                "Delega rifiutata: i tre ruoli statici sono già stati eseguiti.");
        }

        StaticWorkerStep step = StaticHierarchyPlan.Steps[index];
        DelegationOutcome observed = await inner.DelegateAsync(request, cancellationToken);
        bool succeeded = observed.Status == DelegationStatus.Completed
            && !string.IsNullOrWhiteSpace(observed.Text);
        string? reason = succeeded
            ? null
            : observed.Status == DelegationStatus.Completed
                ? "Il lavoratore ha concluso senza riepilogo."
                : observed.Text;
        outcomes.Add(new StaticWorkerOutcome(
            step,
            new StaticWorkerResult(succeeded, observed.Text, reason)));
        if (!succeeded || outcomes.Count == StaticHierarchyPlan.Steps.Count)
        {
            stopped = true;
        }

        return observed;
    }
}
