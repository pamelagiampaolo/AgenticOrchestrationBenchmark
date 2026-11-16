namespace AgenticExperimentRunner;

/// <summary>
/// Interpretazione di argomenti nella forma <c>--opzione valore</c>, comune ai sottocomandi.
/// </summary>
public static class CommandLineOptions
{
    /// <summary>
    /// Interpreta gli argomenti: ogni opzione richiede un valore non vuoto e può comparire una sola volta.
    /// </summary>
    /// <param name="arguments">Argomenti successivi al nome del comando.</param>
    /// <param name="requiredOptions">Opzioni obbligatorie.</param>
    /// <param name="optionalOptions">Opzioni facoltative.</param>
    /// <param name="usage">Sintassi del comando, aggiunta ai messaggi d'errore.</param>
    /// <param name="values">Valori per opzione, se l'interpretazione riesce.</param>
    /// <param name="error">Messaggio diagnostico, se l'interpretazione fallisce.</param>
    /// <returns><see langword="true"/> se gli argomenti sono validi.</returns>
    public static bool TryParse(
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> requiredOptions,
        IReadOnlyList<string> optionalOptions,
        string usage,
        out IReadOnlyDictionary<string, string> values,
        out string? error)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        values = parsed;
        for (int index = 0; index < arguments.Count; index += 2)
        {
            string option = arguments[index];
            if (!requiredOptions.Contains(option) && !optionalOptions.Contains(option))
            {
                error = $"Argomento non riconosciuto: '{option}'. {usage}";
                return false;
            }

            if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                error = $"L'opzione {option} richiede un valore. {usage}";
                return false;
            }

            if (!parsed.TryAdd(option, arguments[index + 1]))
            {
                error = $"L'opzione {option} non può essere specificata più di una volta.";
                return false;
            }
        }

        string? missing = requiredOptions.FirstOrDefault(option => !parsed.ContainsKey(option));
        if (missing is not null)
        {
            error = $"Opzione obbligatoria mancante: {missing}. {usage}";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Restituisce il primo percorso non assoluto tra le opzioni indicate.
    /// </summary>
    /// <param name="values">Valori interpretati.</param>
    /// <param name="pathOptions">Opzioni che contengono percorsi.</param>
    /// <returns>Messaggio d'errore, oppure <see langword="null"/> se tutti i percorsi sono assoluti.</returns>
    public static string? FindRelativePath(IReadOnlyDictionary<string, string> values, IEnumerable<string> pathOptions)
    {
        foreach (string option in pathOptions)
        {
            if (values.TryGetValue(option, out string? path) && !Path.IsPathFullyQualified(path))
            {
                return $"Il percorso fornito a {option} deve essere assoluto: '{path}'.";
            }
        }

        return null;
    }
}
