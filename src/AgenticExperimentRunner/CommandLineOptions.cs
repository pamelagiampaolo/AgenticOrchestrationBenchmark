using System.Globalization;
using AgenticExperimentRunner.SessionExecution;

namespace AgenticExperimentRunner;

/// <summary>
/// Interpretazione di argomenti nella forma <c>--opzione valore</c>, comune ai sottocomandi.
/// </summary>
public static class CommandLineOptions
{
    /// <summary>Opzioni obbligatorie che identificano una run nel piano sperimentale.</summary>
    public static string[] ExperimentMetadataOptions =>
        ["--run-kind", "--repetition", "--order-seed", "--order-position"];

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

    /// <summary>
    /// Interpreta i metadati sperimentali comuni ai comandi di esecuzione.
    /// </summary>
    /// <param name="values">Opzioni già interpretate.</param>
    /// <param name="metadata">Metadati risultanti, se validi.</param>
    /// <param name="error">Diagnostica, se presente.</param>
    /// <returns><see langword="true"/> se tutti i valori sono validi.</returns>
    public static bool TryParseExperimentMetadata(
        IReadOnlyDictionary<string, string> values,
        out ExperimentRunMetadata? metadata,
        out string? error)
    {
        metadata = null;
        ExperimentRunKind kind;
        if (values["--run-kind"] == "pilot")
        {
            kind = ExperimentRunKind.Pilot;
        }
        else if (values["--run-kind"] == "official")
        {
            kind = ExperimentRunKind.Official;
        }
        else
        {
            error = $"--run-kind richiede 'pilot' oppure 'official': '{values["--run-kind"]}'.";
            return false;
        }

        if (!int.TryParse(values["--repetition"], NumberStyles.None, CultureInfo.InvariantCulture, out int repetition)
            || repetition <= 0)
        {
            error = $"--repetition richiede un intero positivo: '{values["--repetition"]}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(values["--order-seed"]))
        {
            error = "--order-seed richiede un valore non vuoto.";
            return false;
        }

        if (!int.TryParse(values["--order-position"], NumberStyles.None, CultureInfo.InvariantCulture, out int orderPosition)
            || orderPosition <= 0)
        {
            error = $"--order-position richiede un intero positivo: '{values["--order-position"]}'.";
            return false;
        }

        metadata = new ExperimentRunMetadata(kind, repetition, values["--order-seed"], orderPosition);
        error = null;
        return true;
    }
}
