using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Argomenti validati del comando <c>prepare-run</c>.
/// </summary>
/// <param name="RepositoryPath">Percorso assoluto del repository sorgente.</param>
/// <param name="Commit">Riferimento della baseline.</param>
/// <param name="TaskId">Task da preparare.</param>
/// <param name="RunsRootPath">Root assoluta delle aree di run.</param>
/// <param name="ResultsRootPath">Root assoluta dei risultati.</param>
/// <param name="RunId">Identificatore esplicito, oppure <see langword="null"/> per generarlo.</param>
public sealed record PrepareRunArguments(
    string RepositoryPath,
    string Commit,
    string TaskId,
    string RunsRootPath,
    string ResultsRootPath,
    string? RunId)
{
    /// <summary>Sintassi del comando, riportata nei messaggi d'errore.</summary>
    public const string Usage =
        "Uso: prepare-run --repository <assoluto> --commit <riferimento> --task <T1..T6> --runs-root <assoluto> --results-root <assoluto> [--run-id <id>]";

    private static readonly string[] RequiredOptions =
        ["--repository", "--commit", "--task", "--runs-root", "--results-root"];

    private const string RunIdOption = "--run-id";

    /// <summary>
    /// Interpreta gli argomenti successivi al nome del comando.
    /// </summary>
    /// <param name="arguments">Coppie <c>--opzione valore</c>.</param>
    /// <param name="parsed">Argomenti validati, se l'interpretazione riesce.</param>
    /// <param name="error">Messaggio diagnostico, se l'interpretazione fallisce.</param>
    /// <returns><see langword="true"/> se tutti gli argomenti sono validi.</returns>
    /// <remarks>
    /// Qui si verificano solo sintassi e percorsi assoluti; il contenuto (task,
    /// commit, root) è validato dal preparatore.
    /// </remarks>
    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out PrepareRunArguments? parsed,
        out string? error)
    {
        parsed = null;
        if (!CommandLineOptions.TryParse(arguments, RequiredOptions, [RunIdOption], Usage, out IReadOnlyDictionary<string, string> values, out error))
        {
            return false;
        }

        error = CommandLineOptions.FindRelativePath(values, ["--repository", "--runs-root", "--results-root"]);
        if (error is not null)
        {
            return false;
        }

        parsed = new PrepareRunArguments(
            values["--repository"],
            values["--commit"],
            values["--task"],
            values["--runs-root"],
            values["--results-root"],
            values.GetValueOrDefault(RunIdOption));
        return true;
    }
}

/// <summary>
/// Documento JSON scritto su stdout da <c>prepare-run</c>: unica fonte dei percorsi
/// per il launcher della CLI e per il server MCP.
/// </summary>
/// <param name="SchemaVersion">Versione del formato di questo documento.</param>
/// <param name="RunId">Identificatore della run.</param>
/// <param name="TaskId">Task preparato.</param>
/// <param name="SourceCommit">SHA completo del commit della baseline.</param>
/// <param name="RunAreaPath">Directory assoluta della run.</param>
/// <param name="AgentRoot">Root assoluta del server MCP.</param>
/// <param name="SessionDirectory">Directory di lavoro assoluta della CLI dell'agente.</param>
/// <param name="ResultsDirectory">Directory assoluta dei risultati della run.</param>
/// <param name="ManifestPath">Percorso assoluto del manifest.</param>
/// <param name="WorkspaceDirectory">Sorgenti, relativa ad <paramref name="AgentRoot"/>.</param>
/// <param name="AgentTestsDirectory">Test dell'agente, relativa ad <paramref name="AgentRoot"/>.</param>
/// <param name="PublicReferenceDirectory">Test pubblici, relativa ad <paramref name="AgentRoot"/>.</param>
/// <param name="TaskSpecificationPath">Specifica, relativa ad <paramref name="AgentRoot"/>.</param>
/// <param name="WritableDirectories">Valori per <c>MCP_WRITABLE_DIRECTORIES</c>, relativi ad <paramref name="AgentRoot"/>.</param>
public sealed record PrepareRunOutput(
    int SchemaVersion,
    string RunId,
    string TaskId,
    string SourceCommit,
    string RunAreaPath,
    string AgentRoot,
    string SessionDirectory,
    string ResultsDirectory,
    string ManifestPath,
    string WorkspaceDirectory,
    string AgentTestsDirectory,
    string PublicReferenceDirectory,
    string TaskSpecificationPath,
    IReadOnlyList<string> WritableDirectories)
{
    /// <summary>Versione corrente del documento di output.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Opzioni di serializzazione condivise da scrittura e test.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // Fine riga fisso: file e hash non devono dipendere dal sistema operativo.
        NewLine = "\n",
    };
}

/// <summary>
/// Comando <c>prepare-run</c>: prepara un'area di run e ne restituisce i percorsi.
/// </summary>
public sealed class PrepareRunCommand
{
    /// <summary>Nome del sottocomando sulla riga di comando.</summary>
    public const string Name = "prepare-run";

    /// <summary>Exit code di successo.</summary>
    public const int SuccessExitCode = 0;

    /// <summary>Exit code di preparazione fallita.</summary>
    public const int PreparationFailedExitCode = 1;

    /// <summary>Exit code di argomenti non validi.</summary>
    public const int UsageErrorExitCode = 2;

    /// <summary>Sottodirectory del repository che costituisce la baseline del benchmark.</summary>
    public const string BenchmarkSourceDirectory = "benchmark/BenchmarkTarget";

    /// <summary>Copia dello stdout nella directory dei risultati della run.</summary>
    public const string PreparedRunFileName = "prepared-run.json";

    private readonly RunAreaPreparer preparer;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Crea il comando con dipendenze esplicite.
    /// </summary>
    /// <param name="preparer">Preparatore delle aree di run.</param>
    /// <param name="timeProvider">Orologio usato per il <c>runId</c> predefinito.</param>
    public PrepareRunCommand(RunAreaPreparer preparer, TimeProvider timeProvider)
    {
        this.preparer = preparer ?? throw new ArgumentNullException(nameof(preparer));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Esegue il comando. Su stdout scrive soltanto il documento JSON in caso di
    /// successo; ogni diagnostica va su stderr.
    /// </summary>
    /// <param name="arguments">Argomenti successivi al nome del comando.</param>
    /// <param name="standardOutput">Destinazione del JSON.</param>
    /// <param name="standardError">Destinazione della diagnostica.</param>
    /// <param name="cancellationToken">Token di annullamento.</param>
    /// <returns>
    /// <see cref="SuccessExitCode"/>, <see cref="UsageErrorExitCode"/> o
    /// <see cref="PreparationFailedExitCode"/>.
    /// </returns>
    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        if (!PrepareRunArguments.TryParse(arguments, out PrepareRunArguments? parsed, out string? error))
        {
            await standardError.WriteLineAsync($"Errore negli argomenti: {error}");
            return UsageErrorExitCode;
        }

        PreparedRunArea area;
        try
        {
            area = await preparer.PrepareAsync(
                new RunAreaRequest(
                    parsed!.RepositoryPath,
                    parsed.Commit,
                    BenchmarkSourceDirectory,
                    parsed.TaskId,
                    parsed.RunsRootPath,
                    parsed.ResultsRootPath,
                    parsed.RunId ?? CreateDefaultRunId()),
                cancellationToken);
        }
        catch (RunPreparationException exception)
        {
            await standardError.WriteLineAsync($"Preparazione della run non riuscita: {exception.Message}");
            return PreparationFailedExitCode;
        }

        string json = JsonSerializer.Serialize(CreateOutput(area), PrepareRunOutput.SerializerOptions);
        try
        {
            // Copia persistente dello stdout: input del comando che avvia la sessione.
            await using var file = new FileStream(
                Path.Combine(area.ResultsDirectoryPath, PreparedRunFileName), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.WriteAsync(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json + "\n"), cancellationToken);
        }
        catch (IOException exception)
        {
            await standardError.WriteLineAsync($"Preparazione della run non riuscita: scrittura di {PreparedRunFileName} fallita: {exception.Message}");
            return PreparationFailedExitCode;
        }

        await standardOutput.WriteLineAsync(json);
        return SuccessExitCode;
    }

    /// <summary>
    /// Genera un <c>runId</c> ordinabile per data, con suffisso casuale contro le collisioni.
    /// </summary>
    /// <returns>Per esempio <c>20261108T120000Z-1a2b3c4d</c>.</returns>
    public string CreateDefaultRunId() =>
        $"{timeProvider.GetUtcNow().UtcDateTime:yyyyMMdd'T'HHmmss'Z'}-{RandomNumberGenerator.GetHexString(8, lowercase: true)}";

    private static PrepareRunOutput CreateOutput(PreparedRunArea area) =>
        new(
            PrepareRunOutput.CurrentSchemaVersion,
            area.Manifest.RunId,
            area.Manifest.TaskId,
            area.Manifest.SourceCommit,
            area.RunAreaPath,
            area.AgentRootPath,
            area.SessionDirectoryPath,
            area.ResultsDirectoryPath,
            area.ManifestPath,
            RunAreaPreparer.WorkspaceDirectoryName,
            RunAreaPreparer.AgentTestsDirectoryName,
            RunAreaPreparer.PublicReferenceDirectoryName,
            RunAreaPreparer.TaskSpecificationFileName,
            area.Manifest.WritableDirectories);
}
