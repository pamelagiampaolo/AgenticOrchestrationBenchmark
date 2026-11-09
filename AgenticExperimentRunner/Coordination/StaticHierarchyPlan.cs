namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Un passaggio obbligatorio della gerarchia statica.
/// </summary>
/// <param name="AgentId">Identificatore stabile del lavoratore.</param>
/// <param name="Role">Ruolo sperimentale registrato nelle metriche.</param>
/// <param name="InstructionFileName">Istruzione versionata specifica del ruolo.</param>
public sealed record StaticWorkerStep(
    string AgentId,
    string Role,
    string InstructionFileName);

/// <summary>
/// Piano immutabile della gerarchia statica approvata: analisi, implementazione e verifica.
/// </summary>
public static class StaticHierarchyPlan
{
    /// <summary>Nome dell'architettura nei record sperimentali.</summary>
    public const string ArchitectureName = "hierarchical-static";

    /// <summary>Passaggi obbligatori, nell'unico ordine ammesso.</summary>
    public static readonly IReadOnlyList<StaticWorkerStep> Steps =
    [
        new("analyst", "analyst", "static-analyst.md"),
        new("implementer", "implementer", "static-implementer.md"),
        new("verifier", "verifier", "static-verifier.md"),
    ];
}

/// <summary>
/// Esito prodotto da un lavoratore statico.
/// </summary>
/// <param name="Succeeded">Indica se il processo ha completato correttamente il passaggio.</param>
/// <param name="Summary">Riepilogo restituito al passaggio successivo.</param>
/// <param name="FailureReason">Motivo osservato del fallimento.</param>
public sealed record StaticWorkerResult(
    bool Succeeded,
    string? Summary,
    string? FailureReason);

/// <summary>
/// Esito di un passaggio statico associato al relativo ruolo.
/// </summary>
/// <param name="Step">Passaggio eseguito.</param>
/// <param name="Result">Risultato normalizzato.</param>
public sealed record StaticWorkerOutcome(
    StaticWorkerStep Step,
    StaticWorkerResult Result);

/// <summary>
/// Risultato complessivo dello schedulatore statico.
/// </summary>
/// <param name="Completed">Vero soltanto se tutti i passaggi sono riusciti.</param>
/// <param name="Outcomes">Passaggi avviati, nello stesso ordine del piano.</param>
public sealed record StaticHierarchyExecution(
    bool Completed,
    IReadOnlyList<StaticWorkerOutcome> Outcomes);

/// <summary>
/// Esegue meccanicamente i ruoli statici uno alla volta e arresta il piano al primo fallimento.
/// </summary>
public static class StaticHierarchyScheduler
{
    /// <summary>
    /// Esegue i tre passaggi approvati nell'ordine fisso.
    /// </summary>
    /// <param name="runStep">Funzione che avvia un singolo lavoratore.</param>
    /// <param name="cancellationToken">Annullamento dell'intera architettura.</param>
    /// <returns>Esiti dei soli passaggi effettivamente avviati.</returns>
    public static async Task<StaticHierarchyExecution> RunAsync(
        Func<StaticWorkerStep, CancellationToken, Task<StaticWorkerResult>> runStep,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runStep);
        var outcomes = new List<StaticWorkerOutcome>(StaticHierarchyPlan.Steps.Count);
        foreach (StaticWorkerStep step in StaticHierarchyPlan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StaticWorkerResult observed = await runStep(step, cancellationToken);
            StaticWorkerResult normalized = Normalize(observed);
            outcomes.Add(new StaticWorkerOutcome(step, normalized));
            if (!normalized.Succeeded)
            {
                return new StaticHierarchyExecution(false, outcomes);
            }
        }

        return new StaticHierarchyExecution(true, outcomes);
    }

    private static StaticWorkerResult Normalize(StaticWorkerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Succeeded && string.IsNullOrWhiteSpace(result.Summary))
        {
            return new StaticWorkerResult(
                false,
                result.Summary,
                "Il lavoratore ha dichiarato successo senza produrre un riepilogo.");
        }

        if (!result.Succeeded && string.IsNullOrWhiteSpace(result.FailureReason))
        {
            return result with { FailureReason = "Il lavoratore non ha fornito il motivo del fallimento." };
        }

        return result;
    }
}
