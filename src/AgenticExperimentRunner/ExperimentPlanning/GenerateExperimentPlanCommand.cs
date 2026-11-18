using System.Text.Json;
using AgenticExperimentRunner.SessionExecution;

namespace AgenticExperimentRunner.ExperimentPlanning;

/// <summary>Comando puro che genera su stdout il piano JSON delle 54 run ufficiali.</summary>
public static class GenerateExperimentPlanCommand
{
    /// <summary>Nome del sottocomando.</summary>
    public const string Name = "generate-experiment-plan";

    /// <summary>Generazione riuscita.</summary>
    public const int SuccessExitCode = 0;

    /// <summary>Argomenti non validi.</summary>
    public const int UsageErrorExitCode = 2;

    private const string Usage = "Uso: generate-experiment-plan --seed <object-id-git-completo>";

    /// <summary>Interpreta il seed, genera il piano e lo scrive senza creare file.</summary>
    /// <param name="arguments">Argomenti successivi al nome del comando.</param>
    /// <param name="standardOutput">Destinazione JSON.</param>
    /// <param name="standardError">Destinazione diagnostica.</param>
    /// <returns>Exit code.</returns>
    public static async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (!CommandLineOptions.TryParse(arguments, ["--seed"], [], Usage, out IReadOnlyDictionary<string, string> values, out string? error))
        {
            await standardError.WriteLineAsync($"Errore negli argomenti: {error}");
            return UsageErrorExitCode;
        }

        ExperimentPlan plan;
        try
        {
            plan = ExperimentPlanGenerator.Generate(values["--seed"]);
        }
        catch (ArgumentException exception)
        {
            await standardError.WriteLineAsync($"Errore negli argomenti: {exception.Message}");
            return UsageErrorExitCode;
        }

        await standardOutput.WriteLineAsync(JsonSerializer.Serialize(plan, SessionRecord.SerializerOptions));
        return SuccessExitCode;
    }
}
