using System.Globalization;
using System.Text.Json;

namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Comando <c>run-single-agent</c>: esegue una sessione single-agent su un'area preparata.
/// </summary>
public sealed class RunSingleAgentCommand
{
    /// <summary>Nome del sottocomando sulla riga di comando.</summary>
    public const string Name = "run-single-agent";

    /// <summary>Sessione eseguita e registrata (lo stato sperimentale è nel record).</summary>
    public const int SuccessExitCode = 0;

    /// <summary>Sessione non avviata o non registrata.</summary>
    public const int SessionFailedExitCode = 1;

    /// <summary>Argomenti non validi.</summary>
    public const int UsageErrorExitCode = 2;

    private const string Usage =
        "Uso: run-single-agent --prepared-run <assoluto> --claude <assoluto> --mcp-server <assoluto> --node <assoluto> --vitest <assoluto> --typescript <assoluto> --model <id> --time-limit-seconds <n> --token-budget <n> --run-kind <pilot|official> --repetition <n> --order-seed <valore> --order-position <n>";

    private static readonly string[] RequiredOptions =
        ["--prepared-run", "--claude", "--mcp-server", "--node", "--vitest", "--typescript", "--model", "--time-limit-seconds", "--token-budget", .. CommandLineOptions.ExperimentMetadataOptions];

    private readonly SingleAgentSessionRunner runner;

    /// <summary>
    /// Crea il comando.
    /// </summary>
    /// <param name="runner">Runner della sessione.</param>
    public RunSingleAgentCommand(SingleAgentSessionRunner runner)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    /// <summary>
    /// Esegue il comando. Su stdout scrive un JSON con identificatore, stato e percorso
    /// del record; la diagnostica va su stderr.
    /// </summary>
    /// <param name="arguments">Argomenti successivi al nome del comando.</param>
    /// <param name="standardOutput">Destinazione del JSON.</param>
    /// <param name="standardError">Destinazione della diagnostica.</param>
    /// <param name="cancellationToken">Annullamento.</param>
    /// <returns>Exit code.</returns>
    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        if (!TryParse(arguments, out SingleAgentSessionRequest? request, out string? error))
        {
            await standardError.WriteLineAsync($"Errore negli argomenti: {error}");
            return UsageErrorExitCode;
        }

        SessionRecord record;
        string recordPath;
        try
        {
            (record, recordPath) = await runner.RunAsync(request!, cancellationToken);
        }
        catch (SessionExecutionException exception)
        {
            await standardError.WriteLineAsync($"Sessione non eseguita: {exception.Message}");
            return SessionFailedExitCode;
        }

        await standardOutput.WriteLineAsync(JsonSerializer.Serialize(
            new { record.RunId, record.Status, record.TokensObserved, record.AgentTaskDurationMs, SessionRecordPath = recordPath },
            SessionRecord.SerializerOptions));
        return SuccessExitCode;
    }

    /// <summary>
    /// Interpreta gli argomenti.
    /// </summary>
    /// <param name="arguments">Argomenti successivi al nome del comando.</param>
    /// <param name="request">Richiesta, se valida.</param>
    /// <param name="error">Messaggio diagnostico, se non valida.</param>
    /// <returns><see langword="true"/> se gli argomenti sono validi.</returns>
    public static bool TryParse(IReadOnlyList<string> arguments, out SingleAgentSessionRequest? request, out string? error)
    {
        request = null;
        if (!CommandLineOptions.TryParse(arguments, RequiredOptions, [], Usage, out IReadOnlyDictionary<string, string> values, out error))
        {
            return false;
        }

        error = CommandLineOptions.FindRelativePath(values, ["--prepared-run", "--claude", "--mcp-server", "--node", "--vitest", "--typescript"]);
        if (error is not null)
        {
            return false;
        }

        if (!long.TryParse(values["--time-limit-seconds"], NumberStyles.None, CultureInfo.InvariantCulture, out long seconds) || seconds <= 0)
        {
            error = $"--time-limit-seconds richiede un intero positivo: '{values["--time-limit-seconds"]}'.";
            return false;
        }

        if (!long.TryParse(values["--token-budget"], NumberStyles.None, CultureInfo.InvariantCulture, out long budget) || budget <= 0)
        {
            error = $"--token-budget richiede un intero positivo: '{values["--token-budget"]}'.";
            return false;
        }

        error = ExperimentProtocol.FindModelValidationError(values["--model"]);
        if (error is not null)
        {
            return false;
        }

        if (!CommandLineOptions.TryParseExperimentMetadata(values, out ExperimentRunMetadata? metadata, out error))
        {
            return false;
        }

        request = new SingleAgentSessionRequest(
            values["--prepared-run"],
            values["--claude"],
            values["--mcp-server"],
            new McpDevelopmentToolchain(values["--node"], values["--vitest"], values["--typescript"]),
            values["--model"],
            TimeSpan.FromSeconds(seconds),
            budget,
            metadata!);
        return true;
    }
}
