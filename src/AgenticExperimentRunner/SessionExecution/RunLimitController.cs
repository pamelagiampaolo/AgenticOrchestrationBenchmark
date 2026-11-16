namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Controllore esterno dei limiti di una run (B4): un solo budget di <c>totalTokens</c> e
/// un solo limite di tempo per tutti gli agenti della run. Quando un limite è raggiunto,
/// o quando la run deve essere interrotta per un altro motivo, segnala la fermata a tutti
/// i processi tramite <see cref="Stopping"/>.
/// </summary>
/// <remarks>
/// Thread-safe: coordinatore e lavoratori riportano l'uso da attività diverse.
/// La prima causa di fermata determina <see cref="StopReason"/>; i flag dei limiti
/// restano comunque aggiornati anche dopo la fermata.
/// </remarks>
public sealed class RunLimitController : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, TokenUsage> usageByAgent = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource stopping = new();
    private readonly TimeProvider timeProvider;
    private ITimer? deadlineTimer;
    private long startTimestamp;
    private bool started;

    /// <summary>
    /// Crea il controllore, non ancora avviato.
    /// </summary>
    /// <param name="tokenBudget">Budget di <c>totalTokens</c> dell'intera run.</param>
    /// <param name="timeLimit">Limite di <c>agentTaskDurationMs</c> dell'intera run.</param>
    /// <param name="timeProvider">Orologio del cronometro e del timer.</param>
    public RunLimitController(long tokenBudget, TimeSpan timeLimit, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokenBudget);
        if (timeLimit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeLimit), "Il limite di tempo deve essere positivo.");
        }

        TokenBudget = tokenBudget;
        TimeLimit = timeLimit;
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>Budget di token applicato.</summary>
    public long TokenBudget { get; }

    /// <summary>Limite di tempo applicato.</summary>
    public TimeSpan TimeLimit { get; }

    /// <summary>Annullato quando tutti i processi della run devono terminare.</summary>
    public CancellationToken Stopping => stopping.Token;

    /// <summary>Il budget di token è stato raggiunto dall'uso osservato.</summary>
    public bool TokensExceeded { get; private set; }

    /// <summary>Il limite di tempo è stato raggiunto.</summary>
    public bool TimeExceeded { get; private set; }

    /// <summary>Prima causa della fermata, oppure <see langword="null"/>.</summary>
    public string? StopReason { get; private set; }

    /// <summary>Somma dell'uso osservato di tutti gli agenti.</summary>
    public long ObservedTotalTokens
    {
        get
        {
            lock (gate)
            {
                return usageByAgent.Values.Sum(usage => usage.TotalTokens);
            }
        }
    }

    /// <summary>Tempo trascorso dall'avvio, misurato con cronometro monotono.</summary>
    public TimeSpan Elapsed => started ? timeProvider.GetElapsedTime(startTimestamp) : TimeSpan.Zero;

    /// <summary>
    /// Avvia cronometro e timer del limite di tempo. Va chiamato una sola volta,
    /// immediatamente prima di avviare il primo processo.
    /// </summary>
    /// <returns>Istante di avvio.</returns>
    public DateTimeOffset Start()
    {
        lock (gate)
        {
            if (started)
            {
                throw new InvalidOperationException("Il controllore è già stato avviato.");
            }

            started = true;
            startTimestamp = timeProvider.GetTimestamp();
            deadlineTimer = timeProvider.CreateTimer(_ => OnDeadline(), null, TimeLimit, Timeout.InfiniteTimeSpan);
            return timeProvider.GetUtcNow();
        }
    }

    /// <summary>
    /// Registra l'uso osservato finora da un agente (valore cumulativo dell'agente,
    /// non un incremento) e ferma la run se il totale raggiunge il budget.
    /// </summary>
    /// <param name="agentId">Identificatore dell'agente.</param>
    /// <param name="observedUsage">Uso cumulativo osservato nello stream dell'agente.</param>
    public void ReportUsage(string agentId, TokenUsage observedUsage)
    {
        bool cancel;
        lock (gate)
        {
            usageByAgent[agentId] = observedUsage;
            long total = usageByAgent.Values.Sum(usage => usage.TotalTokens);
            if (total < TokenBudget || TokensExceeded)
            {
                return;
            }

            TokensExceeded = true;
            TimeExceeded |= Elapsed >= TimeLimit;
            cancel = RecordStopLocked($"budget di {TokenBudget} token raggiunto");
        }

        CancelIf(cancel);
    }

    /// <summary>
    /// Ferma la run per un motivo diverso dai limiti (per esempio una configurazione non conforme).
    /// </summary>
    /// <param name="reason">Motivo registrato se è la prima causa di fermata.</param>
    public void Stop(string reason)
    {
        bool cancel;
        lock (gate)
        {
            cancel = RecordStopLocked(reason);
        }

        CancelIf(cancel);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        deadlineTimer?.Dispose();
        stopping.Dispose();
    }

    private void OnDeadline()
    {
        bool cancel;
        lock (gate)
        {
            TimeExceeded = true;
            cancel = RecordStopLocked($"limite di tempo di {TimeLimit.TotalSeconds} secondi raggiunto");
        }

        CancelIf(cancel);
    }

    /// <summary>Registra la prima causa di fermata; restituisce se occorre annullare.</summary>
    private bool RecordStopLocked(string reason)
    {
        if (StopReason is not null)
        {
            return false;
        }

        StopReason = reason;
        return true;
    }

    /// <summary>
    /// Annulla fuori dal lock: i callback registrati su <see cref="Stopping"/> vengono
    /// eseguiti in modo sincrono e non devono girare mentre il lock è tenuto.
    /// </summary>
    private void CancelIf(bool cancel)
    {
        if (!cancel)
        {
            return;
        }

        try
        {
            stopping.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Run già chiusa: nessun processo da fermare.
        }
    }
}
