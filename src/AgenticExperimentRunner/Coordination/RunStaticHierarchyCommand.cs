using System.Globalization;
using System.Text.Json;
using AgenticExperimentRunner.SessionExecution;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Comando <c>run-static-hierarchy</c>: esegue la gerarchia con analyst,
/// implementer e verifier obbligatori.
/// </summary>
public sealed class RunStaticHierarchyCommand
{
    /// <summary>Nome del sottocomando.</summary>
    public const string Name = "run-static-hierarchy";

    /// <summary>Run eseguita e registrata.</summary>
    public const int SuccessExitCode = 0;

    /// <summary>Run non avviata o non registrata.</summary>
    public const int RunFailedExitCode = 1;

    /// <summary>Argomenti non validi.</summary>
    public const int UsageErrorExitCode = 2;

    private const string Usage =
        "Uso: run-static-hierarchy --prepared-run <assoluto> --claude <assoluto> --mcp-server <assoluto> --node <assoluto> --vitest <assoluto> --typescript <assoluto> --model <id> --time-limit-seconds <n> --token-budget <n>";

    private static readonly string[] RequiredOptions =
        ["--prepared-run", "--claude", "--mcp-server", "--node", "--vitest", "--typescript", "--model", "--time-limit-seconds", "--token-budget"];

    private readonly HierarchicalRunRunner runner;
    private readonly string coordinationServerPath;

    /// <summary>Crea il comando.</summary>
    /// <param name="runner">Runner configurato in modalità statica.</param>
    /// <param name="coordinationServerPath">Eseguibile usato dalla CLI come relay.</param>
    public RunStaticHierarchyCommand(HierarchicalRunRunner runner, string coordinationServerPath)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinationServerPath);
        this.coordinationServerPath = coordinationServerPath;
    }

    /// <summary>Esegue il comando e scrive il riepilogo JSON su stdout.</summary>
    /// <param name="arguments">Argomenti successivi al nome del comando.</param>
    /// <param name="standardOutput">Destinazione del riepilogo.</param>
    /// <param name="standardError">Destinazione della diagnostica.</param>
    /// <param name="cancellationToken">Annullamento della run.</param>
    /// <returns>Exit code del comando.</returns>
    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        if (!TryParse(arguments, coordinationServerPath, out HierarchicalRunRequest? request, out string? error))
        {
            await standardError.WriteLineAsync($"Errore negli argomenti: {error}");
            return UsageErrorExitCode;
        }

        HierarchicalRunRecord record;
        string recordPath;
        try
        {
            (record, recordPath) = await runner.RunAsync(request!, cancellationToken);
        }
        catch (Exception exception) when (exception is SessionExecutionException or CoordinationChannelException)
        {
            await standardError.WriteLineAsync($"Run non eseguita: {exception.Message}");
            return RunFailedExitCode;
        }

        await standardOutput.WriteLineAsync(JsonSerializer.Serialize(
            new
            {
                record.RunId,
                record.Status,
                record.TokensObserved,
                record.AgentTaskDurationMs,
                record.DelegationCounts,
                RunRecordPath = recordPath,
            },
            SessionRecord.SerializerOptions));
        return SuccessExitCode;
    }

    /// <summary>Interpreta gli argomenti senza avviare processi.</summary>
    /// <param name="arguments">Argomenti successivi al nome del comando.</param>
    /// <param name="coordinationServerPath">Percorso del relay.</param>
    /// <param name="request">Richiesta risultante.</param>
    /// <param name="error">Errore, se presente.</param>
    /// <returns>Vero se gli argomenti sono validi.</returns>
    public static bool TryParse(
        IReadOnlyList<string> arguments,
        string coordinationServerPath,
        out HierarchicalRunRequest? request,
        out string? error)
    {
        request = null;
        if (!CommandLineOptions.TryParse(arguments, RequiredOptions, [], Usage, out IReadOnlyDictionary<string, string> values, out error))
        {
            return false;
        }

        error = CommandLineOptions.FindRelativePath(
            values,
            ["--prepared-run", "--claude", "--mcp-server", "--node", "--vitest", "--typescript"]);
        if (error is not null)
        {
            return false;
        }

        if (!TryParsePositive(values, "--time-limit-seconds", out long seconds, out error)
            || !TryParsePositive(values, "--token-budget", out long budget, out error))
        {
            return false;
        }

        request = new HierarchicalRunRequest(
            values["--prepared-run"],
            values["--claude"],
            values["--mcp-server"],
            new McpDevelopmentToolchain(values["--node"], values["--vitest"], values["--typescript"]),
            coordinationServerPath,
            values["--model"],
            TimeSpan.FromSeconds(seconds),
            budget,
            StaticHierarchyPlan.Steps.Count);
        return true;
    }

    private static bool TryParsePositive(
        IReadOnlyDictionary<string, string> values,
        string option,
        out long value,
        out string? error)
    {
        if (long.TryParse(values[option], NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0)
        {
            error = null;
            return true;
        }

        error = $"{option} richiede un intero positivo: '{values[option]}'.";
        return false;
    }
}
