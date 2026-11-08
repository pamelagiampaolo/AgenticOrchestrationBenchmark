using System.Formats.Tar;
using System.Text.RegularExpressions;

namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Parametri di preparazione di un'area di run.
/// </summary>
/// <param name="RepositoryPath">Percorso assoluto di una directory del repository sorgente.</param>
/// <param name="BaselineReference">Commit, tag o branch della baseline approvata.</param>
/// <param name="SourceDirectory">Sottodirectory del repository da copiare, con separatore <c>/</c> (per esempio <c>BenchmarkTarget</c>).</param>
/// <param name="RunsRootPath">Percorso assoluto, esterno al repository, che contiene le aree di run.</param>
/// <param name="RunId">Identificatore della run: lettere, cifre, <c>-</c> e <c>_</c>, al massimo 64 caratteri.</param>
public sealed record RunAreaRequest(
    string RepositoryPath,
    string BaselineReference,
    string SourceDirectory,
    string RunsRootPath,
    string RunId);

/// <summary>
/// Area di run preparata e pronta per essere assegnata a un'architettura.
/// </summary>
/// <param name="RunAreaPath">Directory della run, che contiene workspace e manifest.</param>
/// <param name="WorkspacePath">Unica directory da assegnare all'agente.</param>
/// <param name="ManifestPath">Manifest della baseline, esterno alla workspace.</param>
/// <param name="Manifest">Contenuto del manifest.</param>
public sealed record PreparedRunArea(
    string RunAreaPath,
    string WorkspacePath,
    string ManifestPath,
    BaselineManifest Manifest);

/// <summary>
/// Prepara per ogni run una workspace nuova che contiene soltanto i file versionati
/// della baseline nel commit approvato, insieme al relativo manifest.
/// </summary>
public sealed partial class RunAreaPreparer
{
    /// <summary>
    /// Nome della directory, interna all'area di run, assegnata all'agente.
    /// </summary>
    public const string WorkspaceDirectoryName = "workspace";

    /// <summary>
    /// Nome del manifest, collocato accanto alla workspace e non al suo interno.
    /// </summary>
    public const string ManifestFileName = "baseline-manifest.json";

    private readonly GitCommandRunner git;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Crea un preparatore con dipendenze esplicite.
    /// </summary>
    /// <param name="git">Esecutore dei comandi Git.</param>
    /// <param name="timeProvider">Sorgente dell'orario registrato nel manifest.</param>
    public RunAreaPreparer(GitCommandRunner git, TimeProvider timeProvider)
    {
        this.git = git ?? throw new ArgumentNullException(nameof(git));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Crea l'area di run, vi estrae la baseline e scrive il manifest.
    /// </summary>
    /// <param name="request">Parametri della preparazione.</param>
    /// <param name="cancellationToken">Token di annullamento.</param>
    /// <returns>L'area preparata.</returns>
    /// <exception cref="RunPreparationException">
    /// Parametri non validi, area già esistente, riferimento inesistente o baseline
    /// con contenuto non autorizzato. In caso di errore successivo alla creazione,
    /// l'area viene rimossa.
    /// </exception>
    public async Task<PreparedRunArea> PrepareAsync(
        RunAreaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        string sourceDirectory = WorkspacePathPolicy.NormalizeAuthorizedPath(request.SourceDirectory);

        string repositoryRoot = await ResolveRepositoryRootAsync(request.RepositoryPath, cancellationToken);
        string commit = await ResolveCommitAsync(repositoryRoot, request.BaselineReference, cancellationToken);
        string runsRoot = Path.GetFullPath(request.RunsRootPath);
        RequireOutsideRepository(runsRoot, repositoryRoot);

        string runAreaPath = Path.Combine(runsRoot, request.RunId);
        CreateNewRunArea(runsRoot, runAreaPath);

        try
        {
            string workspacePath = Path.Combine(runAreaPath, WorkspaceDirectoryName);
            Directory.CreateDirectory(workspacePath);
            await ExtractBaselineAsync(repositoryRoot, commit, sourceDirectory, workspacePath, cancellationToken);

            BaselineManifest manifest = BaselineManifest.CreateFromWorkspace(
                workspacePath,
                request.RunId,
                timeProvider.GetUtcNow(),
                commit,
                sourceDirectory);
            if (manifest.FileCount == 0)
            {
                throw new RunPreparationException(
                    $"La sottodirectory '{sourceDirectory}' del commit {commit} non contiene file.");
            }

            string manifestPath = Path.Combine(runAreaPath, ManifestFileName);
            manifest.WriteTo(manifestPath);
            return new PreparedRunArea(runAreaPath, workspacePath, manifestPath, manifest);
        }
        catch (Exception exception)
        {
            RemoveIncompleteRunArea(runAreaPath, exception);
            if (exception is IOException or UnauthorizedAccessException)
            {
                throw new RunPreparationException(
                    $"Scrittura dell'area di run non riuscita: {exception.Message}",
                    exception);
            }

            throw;
        }
    }

    private static void ValidateRequest(RunAreaRequest request)
    {
        if (!RunIdPattern().IsMatch(request.RunId ?? string.Empty))
        {
            throw new RunPreparationException(
                $"RunId non valido: '{request.RunId}'. Sono ammessi lettere, cifre, '-' e '_' (massimo 64 caratteri, iniziale alfanumerica).");
        }

        if (string.IsNullOrWhiteSpace(request.BaselineReference))
        {
            throw new RunPreparationException("Il riferimento della baseline deve essere specificato.");
        }

        RequireFullyQualified(request.RepositoryPath, "repository sorgente");
        RequireFullyQualified(request.RunsRootPath, "root delle run");
    }

    private static void RequireFullyQualified(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new RunPreparationException($"Il percorso del {description} deve essere assoluto: '{path}'.");
        }
    }

    private async Task<string> ResolveRepositoryRootAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(repositoryPath))
        {
            throw new RunPreparationException($"Il repository sorgente non esiste: '{repositoryPath}'.");
        }

        string topLevel = await RunGitAsync(repositoryPath, ["rev-parse", "--show-toplevel"], cancellationToken);
        return Path.GetFullPath(topLevel);
    }

    private async Task<string> ResolveCommitAsync(
        string repositoryRoot,
        string baselineReference,
        CancellationToken cancellationToken)
    {
        // '^{commit}' rifiuta riferimenti che non indicano un commit; '--end-of-options'
        // impedisce che un riferimento venga interpretato come opzione.
        return await RunGitAsync(
            repositoryRoot,
            ["rev-parse", "--verify", "--end-of-options", baselineReference + "^{commit}"],
            cancellationToken);
    }

    private async Task<string> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return await git.RunForTextAsync(workingDirectory, arguments, cancellationToken);
        }
        catch (GitCommandException exception)
        {
            throw new RunPreparationException($"Lettura della baseline non riuscita. {exception.Message}", exception);
        }
    }

    private static void RequireOutsideRepository(string runsRoot, string repositoryRoot)
    {
        string relativePath = Path.GetRelativePath(repositoryRoot, runsRoot);
        bool isInside = relativePath == "."
            || !(relativePath == ".."
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || Path.IsPathFullyQualified(relativePath));

        if (isInside)
        {
            throw new RunPreparationException(
                $"La root delle run '{runsRoot}' si trova nel repository '{repositoryRoot}': i processi della run potrebbero risalire alla cronologia Git.");
        }
    }

    private static void CreateNewRunArea(string runsRoot, string runAreaPath)
    {
        Directory.CreateDirectory(runsRoot);
        if (Directory.Exists(runAreaPath) || File.Exists(runAreaPath))
        {
            throw new RunPreparationException(
                $"L'area di run '{runAreaPath}' esiste già: ogni run richiede un'area nuova.");
        }

        Directory.CreateDirectory(runAreaPath);
    }

    private async Task ExtractBaselineAsync(
        string repositoryRoot,
        string commit,
        string sourceDirectory,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        try
        {
            // 'git archive' legge i blob del commit, quindi ignora albero di lavoro e
            // file non versionati. Applica però la conversione dei fine riga della
            // configurazione host (Git for Windows imposta core.autocrlf=true a livello
            // di sistema): la si neutralizza perché la workspace contenga i byte esatti
            // dei blob, uguali su qualsiasi macchina.
            await git.RunWithOutputStreamAsync(
                repositoryRoot,
                [
                    "-c", "core.autocrlf=false",
                    "-c", "core.eol=lf",
                    "archive", "--format=tar", $"{commit}:{sourceDirectory}",
                ],
                (archive, token) => ExtractArchiveAsync(archive, workspacePath, token),
                cancellationToken);
        }
        catch (GitCommandException exception)
        {
            throw new RunPreparationException($"Estrazione della baseline non riuscita. {exception.Message}", exception);
        }
        catch (InvalidDataException exception)
        {
            throw new RunPreparationException($"L'archivio della baseline non è valido: {exception.Message}", exception);
        }
    }

    private static async Task ExtractArchiveAsync(Stream archive, string workspacePath, CancellationToken cancellationToken)
    {
        await using var reader = new TarReader(archive, leaveOpen: true);
        while (await reader.GetNextEntryAsync(copyData: false, cancellationToken) is { } entry)
        {
            switch (entry.EntryType)
            {
                case TarEntryType.GlobalExtendedAttributes:
                    // Intestazione pax di Git con l'identificatore del commit: non è un file.
                    continue;
                case TarEntryType.Directory:
                    Directory.CreateDirectory(ResolveDestination(workspacePath, entry.Name));
                    continue;
                case TarEntryType.RegularFile or TarEntryType.V7RegularFile:
                    await WriteFileAsync(entry, ResolveDestination(workspacePath, entry.Name), cancellationToken);
                    continue;
                default:
                    throw new RunPreparationException(
                        $"La baseline contiene una voce di tipo {entry.EntryType} non ammessa (sono consentiti solo file e directory): '{entry.Name}'.");
            }
        }
    }

    private static string ResolveDestination(string workspacePath, string archivePath)
    {
        string authorizedPath = WorkspacePathPolicy.NormalizeAuthorizedPath(archivePath);
        string destination = Path.GetFullPath(
            Path.Combine(workspacePath, authorizedPath.Replace('/', Path.DirectorySeparatorChar)));

        // Controllo difensivo indipendente dalla politica: la destinazione deve restare nella workspace.
        if (!destination.StartsWith(workspacePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new RunPreparationException($"La voce '{archivePath}' risolve fuori dalla workspace.");
        }

        return destination;
    }

    private static async Task WriteFileAsync(TarEntry entry, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (entry.DataStream is not null)
        {
            await entry.DataStream.CopyToAsync(output, cancellationToken);
        }
    }

    private static void RemoveIncompleteRunArea(string runAreaPath, Exception originalException)
    {
        try
        {
            if (Directory.Exists(runAreaPath))
            {
                Directory.Delete(runAreaPath, recursive: true);
            }
        }
        catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
        {
            throw new RunPreparationException(
                $"Preparazione fallita ({originalException.Message}) e rimozione dell'area incompleta '{runAreaPath}' non riuscita: {cleanupException.Message}. L'area non deve essere usata.",
                originalException);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$")]
    private static partial Regex RunIdPattern();
}
