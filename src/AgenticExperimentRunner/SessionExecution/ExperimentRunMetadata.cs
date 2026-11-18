namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Classifica una run rispetto al protocollo sperimentale.
/// </summary>
public enum ExperimentRunKind
{
    /// <summary>Run di calibrazione, esclusa dai risultati ufficiali.</summary>
    Pilot,

    /// <summary>Run appartenente alla matrice sperimentale ufficiale.</summary>
    Official,
}

/// <summary>
/// Identità della run nel piano sperimentale, necessaria per separare piloti,
/// ripetizioni e posizione nell'ordine preregistrato.
/// </summary>
/// <param name="Kind">Classificazione pilota o ufficiale.</param>
/// <param name="Repetition">Ripetizione della coppia task–architettura, a partire da 1.</param>
/// <param name="OrderSeed">Seed testuale con cui è stato generato l'ordine.</param>
/// <param name="OrderPosition">Posizione globale della run nell'ordine, a partire da 1.</param>
public sealed record ExperimentRunMetadata(
    ExperimentRunKind Kind,
    int Repetition,
    string OrderSeed,
    int OrderPosition)
{
    /// <summary>
    /// Verifica i vincoli indipendenti dalla riga di comando.
    /// </summary>
    /// <returns>Un messaggio diagnostico, oppure <see langword="null"/> se i dati sono validi.</returns>
    public string? FindValidationError()
    {
        if (!Enum.IsDefined(Kind))
        {
            return $"Tipo di run non valido: '{Kind}'.";
        }

        if (Repetition <= 0)
        {
            return "La ripetizione deve essere un intero positivo.";
        }

        if (string.IsNullOrWhiteSpace(OrderSeed))
        {
            return "Il seed dell'ordine deve essere specificato.";
        }

        if (OrderPosition <= 0)
        {
            return "La posizione nell'ordine deve essere un intero positivo.";
        }

        return null;
    }
}
