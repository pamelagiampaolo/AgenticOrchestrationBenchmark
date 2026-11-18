namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Valori congelati che identificano il protocollo comune a piloti e run ufficiali.
/// </summary>
public static class ExperimentProtocol
{
    /// <summary>Identificatore esatto del modello richiesto a ogni agente.</summary>
    public const string RequiredModel = "claude-sonnet-5-5";

    /// <summary>
    /// Verifica il modello con confronto ordinale: alias e differenze di maiuscole non
    /// sono equivalenti a una condizione sperimentale congelata.
    /// </summary>
    /// <param name="model">Identificatore da verificare.</param>
    /// <returns>Diagnostica, oppure <see langword="null"/> se il modello è quello richiesto.</returns>
    public static string? FindModelValidationError(string? model) =>
        string.Equals(model, RequiredModel, StringComparison.Ordinal)
            ? null
            : $"--model deve essere esattamente '{RequiredModel}': ricevuto '{model}'.";
}
