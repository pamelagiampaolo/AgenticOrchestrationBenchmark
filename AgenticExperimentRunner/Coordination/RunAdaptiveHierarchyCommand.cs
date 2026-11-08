using System.Globalization;
using System.Text.Json;
using AgenticExperimentRunner.SessionExecution;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Comando <c>run-adaptive-hierarchy</c>: esegue una run della gerarchia adattiva su
/// un'area preparata.
/// </summary>
public sealed class RunAdaptiveHierarchyCommand
{
    /// <summary>Nome del sottocomando sulla riga di comando.</summary>
    public const string Name = "run-adaptive-hierarchy";

    /// <summary>Run eseguita e registrata (lo stato sperimentale è nel record).</summary>
    public const int SuccessExitCode = 0;

    /// <summary>Run non avviata o non registrata.</summary>
    public const int RunFailedExitCode = 1;

    /// <summary>Argomenti non validi.</summary>
    public const int UsageErrorExitCode = 2;

    private const string Usage =
        "Uso: run-adaptive-hierarchy --prepared-run <assoluto> --claude <assoluto> --mcp-server <assoluto> --model <id> --time-limit-seconds <n> --token-budget <n> --max-delegations <n>";

    private static readonly string[] RequiredOptions =
        ["--prepared-run", "--claude", "--mcp-server", "--model", "--time-limit-seconds", "--token-budget", "--max-delegations"];

    private readonly HierarchicalRunRunner runner;
    private readonly string coordinationServerPath;

    /// <summary>
    /// Crea il comando.
    /// </summary>
    /// <param name="runner">Runner della run.</param>
    /// <param name="coordinationServerPath">Eseguibile dell'orchestratore, che la CLI avvia come relay.</param>
    public RunAdaptiveHierarchyCommand(HierarchicalRunRunner runner, string coordinationServerPath)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinationServerPath);
        this.coordinationServerPath = coordinationServerPath;
    }

    /// <summary>
    /// Esegue il comando. Su stdout scrive un JSON con identificatore, stato, conteggi delle
    /// deleghe e percorso del record; la diagnostica va su stderr.
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

    /// <summary>
    /// Interpreta gli argomenti.
    /// </summary>
    /// <param name="arguments">Argomenti successivi al nome del comando.</param>
    /// <param name="coordinationServerPath">Eseguibile del relay.</param>
    /// <param name="request">Richiesta, se valida.</param>
    /// <param name="error">Messaggio diagnostico, se non valida.</param>
    /// <returns><see langword="true"/> se gli argomenti sono validi.</returns>
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

        error = CommandLineOptions.FindRelativePath(values, ["--prepared-run", "--claude", "--mcp-server"]);
        if (error is not null)
        {
            return false;
        }

        if (!TryParsePositive(values, "--time-limit-seconds", out long seconds, out error)
            || !TryParsePositive(values, "--token-budget", out long budget, out error)
            || !TryParsePositive(values, "--max-delegations", out long maxDelegations, out error))
        {
            return false;
        }

        if (maxDelegations > int.MaxValue)
        {
            error = $"--max-delegations è troppo grande: '{values["--max-delegations"]}'.";
            return false;
        }

        request = new HierarchicalRunRequest(
            values["--prepared-run"],
            values["--claude"],
            values["--mcp-server"],
            coordinationServerPath,
            values["--model"],
            TimeSpan.FromSeconds(seconds),
            budget,
            (int)maxDelegations);
        return true;
    }

    private static bool TryParsePositive(IReadOnlyDictionary<string, string> values, string option, out long value, out string? error)
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
