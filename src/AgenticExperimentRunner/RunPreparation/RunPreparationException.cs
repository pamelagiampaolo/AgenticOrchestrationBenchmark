namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Segnala che un'area di run non può essere preparata in modo conforme alle regole
/// sperimentali (input non valido, baseline non leggibile o contenuto non autorizzato).
/// </summary>
public sealed class RunPreparationException : Exception
{
    /// <summary>
    /// Crea l'eccezione con un messaggio diagnostico.
    /// </summary>
    /// <param name="message">Descrizione del motivo del rifiuto.</param>
    public RunPreparationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Crea l'eccezione con un messaggio diagnostico e la causa originale.
    /// </summary>
    /// <param name="message">Descrizione del motivo del rifiuto.</param>
    /// <param name="innerException">Errore che ha impedito la preparazione.</param>
    public RunPreparationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
