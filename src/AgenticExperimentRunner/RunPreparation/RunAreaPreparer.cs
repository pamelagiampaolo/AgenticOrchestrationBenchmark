using System.Formats.Tar;
using System.Text.RegularExpressions;

namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Parametri di preparazione di un'area di run.
/// </summary>
/// <param name="RepositoryPath">Percorso assoluto di una directory del repository sorgente.</param>
/// <param name="BaselineReference">Commit, tag o branch della baseline approvata.</param>
/// <param name="SourceDirectory">Sottodirectory del repository usata come baseline, con separatore <c>/</c> (per esempio <c>benchmark/BenchmarkTarget</c>).</param>
/// <param name="TaskId">Task da preparare (<c>T1</c>, <c>T2</c>, ...).</param>
/// <param name="RunsRootPath">Percorso assoluto, esterno al repository, che contiene le aree di run temporanee.</param>
/// <param name="ResultsRootPath">Percorso assoluto, esterno al repository e alle aree di run, che contiene i dati persistenti.</param>
/// <param name="RunId">Identificatore della run: lettere, cifre, <c>-</c> e <c>_</c>, al massimo 64 caratteri.</param>
public sealed record RunAreaRequest(
    string RepositoryPath,
    string BaselineReference,
    string SourceDirectory,
    string TaskId,
    string RunsRootPath,
    string ResultsRootPath,
    string RunId);

/// <summary>
/// Area di run preparata e pronta per essere assegnata a un'architettura.
/// </summary>
/// <param name="RunAreaPath">Directory della run, che contiene <c>agent-root</c> e <c>session</c>.</param>
/// <param name="AgentRootPath">Root del server MCP: unica area di file visibile all'agente.</param>
/// <param name="SessionDirectoryPath">Directory di lavoro della CLI dell'agente, inizialmente vuota.</param>
/// <param name="ResultsDirectoryPath">Directory persistente dei risultati della run.</param>
/// <param name="ManifestPath">Manifest della run, nella directory dei risultati.</param>
/// <param name="Manifest">Contenuto del manifest.</param>
public sealed record PreparedRunArea(
    string RunAreaPath,
    string AgentRootPath,
    string SessionDirectoryPath,
    string ResultsDirectoryPath,
    string ManifestPath,
    RunManifest Manifest);

/// <summary>
/// Prepara per ogni run un'area agente nuova, ricavata interamente da un solo commit:
/// baseline modificabile, test pubblici selezionati, specifica del task e manifest.
/// </summary>
public sealed partial class RunAreaPreparer
{
    /// <summary>Directory dell'area di run usata come root MCP.</summary>
    public const string AgentRootDirectoryName = "agent-root";

    /// <summary>Directory dell'area di run usata come cwd della CLI dell'agente.</summary>
    public const string SessionDirectoryName = "session";

    /// <summary>Sorgenti modificabili, relativa ad <c>agent-root</c>.</summary>
    public const string WorkspaceDirectoryName = "workspace";

    /// <summary>Test pubblici in sola lettura, relativa ad <c>agent-root</c>.</summary>
    public const string PublicReferenceDirectoryName = "public-reference";

    /// <summary>Test aggiuntivi scritti dall'agente, relativa ad <c>agent-root</c>.</summary>
    public const string AgentTestsDirectoryName = "agent-tests";

    /// <summary>Specifica pubblica del task, relativa ad <c>agent-root</c>.</summary>
    public const string TaskSpecificationFileName = "task-specification.md";

    /// <summary>Nome del manifest nella directory dei risultati della run.</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>Directory di <c>agent-root</c> in cui l'agente può scrivere, nel formato di <c>MCP_WRITABLE_DIRECTORIES</c>.</summary>
    public static readonly IReadOnlyList<string> WritableDirectories = [WorkspaceDirectoryName, AgentTestsDirectoryName];

    private readonly GitCommandRunner git;
    private readonly TimeProvider timeProvider;
    private readonly string? userProfileDirectory;

    /// <summary>
    /// Crea un preparatore con dipendenze esplicite.
    /// </summary>
    /// <param name="git">Esecutore dei comandi Git.</param>
    /// <param name="timeProvider">Sorgente dell'orario registrato nel manifest.</param>
    /// <param name="userProfileDirectory">
    /// Profilo dell'utente, in cui la directory <c>.claude</c> è tollerata dal
    /// controllo delle directory superiori (vedi <see cref="SessionAncestorPolicy"/>).
    /// </param>
    public RunAreaPreparer(GitCommandRunner git, TimeProvider timeProvider, string? userProfileDirectory)
    {
        this.git = git ?? throw new ArgumentNullException(nameof(git));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.userProfileDirectory = userProfileDirectory;
    }

    /// <summary>
    /// Crea l'area di run e la directory dei risultati, vi estrae i materiali del
    /// task dal commit indicato e scrive il manifest.
    /// </summary>
    /// <param name="request">Parametri della preparazione.</param>
    /// <param name="cancellationToken">Token di annullamento.</param>
    /// <returns>L'area preparata.</returns>
    /// <exception cref="RunPreparationException">
    /// Parametri non validi, root non ammesse, aree già esistenti, riferimento o
    /// materiali del task inesistenti, oppure baseline con contenuto non autorizzato.
    /// In caso di errore successivo alla creazione, le directory create vengono rimosse.
    /// </exception>
    public async Task<PreparedRunArea> PrepareAsync(
        RunAreaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        string sourceDirectory = WorkspacePathPolicy.NormalizeAuthorizedPath(request.SourceDirectory);
        string taskId = TaskMaterialSelection.NormalizeTaskId(request.TaskId);
        string specificationSource = TaskMaterialSelection.TaskSpecificationSourcePath(taskId);

        string repositoryRoot = await ResolveRepositoryRootAsync(request.RepositoryPath, cancellationToken);
        string commit = await ResolveCommitAsync(repositoryRoot, request.BaselineReference, cancellationToken);
        await RequireObjectTypeAsync(repositoryRoot, commit, specificationSource, "blob", "specifica del task", cancellationToken);
        await RequireObjectTypeAsync(
            repositoryRoot,
            commit,
            $"{sourceDirectory}/{TaskMaterialSelection.TaskPublicTestsDirectory(taskId)}",
            "tree",
            "suite pubblica del task",
            cancellationToken);

        string runsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.RunsRootPath));
        string resultsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.ResultsRootPath));
        ValidateRoots(runsRoot, resultsRoot, repositoryRoot);

        string runAreaPath = Path.Combine(runsRoot, request.RunId);
        string resultsDirectory = Path.Combine(resultsRoot, request.RunId);
        RequireNotExisting(runAreaPath, "L'area di run");
        RequireNotExisting(resultsDirectory, "La directory dei risultati");

        var createdDirectories = new List<string>();
        try
        {
            CreateNewDirectory(runAreaPath, createdDirectories);
            CreateNewDirectory(resultsDirectory, createdDirectories);

            string agentRoot = Path.Combine(runAreaPath, AgentRootDirectoryName);
            string sessionDirectory = Path.Combine(runAreaPath, SessionDirectoryName);
            foreach (string directory in new[]
            {
                Path.Combine(agentRoot, WorkspaceDirectoryName),
                Path.Combine(agentRoot, PublicReferenceDirectoryName),
                Path.Combine(agentRoot, AgentTestsDirectoryName),
                sessionDirectory,
            })
            {
                Directory.CreateDirectory(directory);
            }

            var extraction = new ExtractionState(agentRoot, sourceDirectory, taskId);
            await ExtractBaselineAsync(repositoryRoot, commit, extraction, cancellationToken);
            if (extraction.WorkspaceFileCount == 0)
            {
                throw new RunPreparationException(
                    $"La sottodirectory '{sourceDirectory}' del commit {commit} non contiene file per la workspace.");
            }

            await WriteTaskSpecificationAsync(repositoryRoot, commit, specificationSource, agentRoot, cancellationToken);
            extraction.SourcePaths[TaskSpecificationFileName] = specificationSource;

            IReadOnlyList<RunManifestFile> files = RunManifest.DescribeAgentRoot(agentRoot, extraction.SourcePaths);
            var manifest = new RunManifest(
                RunManifest.CurrentSchemaVersion,
                request.RunId,
                taskId,
                timeProvider.GetUtcNow().ToUniversalTime(),
                commit,
                sourceDirectory,
                specificationSource,
                WritableDirectories,
                files.Count,
                RunManifest.ComputeAggregateSha256(files),
                files,
                extraction.ExcludedSourcePaths.Order(StringComparer.Ordinal).ToList());

            string manifestPath = Path.Combine(resultsDirectory, ManifestFileName);
            manifest.WriteTo(manifestPath);
            return new PreparedRunArea(runAreaPath, agentRoot, sessionDirectory, resultsDirectory, manifestPath, manifest);
        }
        catch (Exception exception)
        {
            RemoveCreatedDirectories(createdDirectories, exception);
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
        RequireFullyQualified(request.ResultsRootPath, "root dei risultati");
    }

    private static void RequireFullyQualified(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new RunPreparationException($"Il percorso della {description} deve essere assoluto: '{path}'.");
        }
    }

    private void ValidateRoots(string runsRoot, string resultsRoot, string repositoryRoot)
    {
        if (IsSameOrInside(runsRoot, repositoryRoot) || IsSameOrInside(resultsRoot, repositoryRoot))
        {
            throw new RunPreparationException(
                $"Le root delle run ('{runsRoot}') e dei risultati ('{resultsRoot}') devono essere esterne al repository '{repositoryRoot}': i processi della run potrebbero risalire alla cronologia Git.");
        }

        if (IsSameOrInside(runsRoot, resultsRoot) || IsSameOrInside(resultsRoot, runsRoot))
        {
            throw new RunPreparationException(
                $"Le root delle run ('{runsRoot}') e dei risultati ('{resultsRoot}') devono essere disgiunte: il manifest non deve trovarsi nell'area delle run, né le run tra i risultati.");
        }

        IReadOnlyList<string> conflicts = SessionAncestorPolicy.FindConflicts(runsRoot, userProfileDirectory);
        if (conflicts.Count > 0)
        {
            throw new RunPreparationException(
                $"La root delle run '{runsRoot}' o una directory superiore contiene istruzioni, configurazioni o metadati Git che la CLI dell'agente potrebbe caricare: {string.Join(", ", conflicts)}.");
        }
    }

    private static bool IsSameOrInside(string candidate, string container)
    {
        string relativePath = Path.GetRelativePath(container, candidate);
        return relativePath == "."
            || !(relativePath == ".."
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || Path.IsPathFullyQualified(relativePath));
    }

    private async Task<string> ResolveRepositoryRootAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(repositoryPath))
        {
            throw new RunPreparationException($"Il repository sorgente non esiste: '{repositoryPath}'.");
        }

        string topLevel = await RunGitAsync(repositoryPath, ["rev-parse", "--show-toplevel"], cancellationToken);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(topLevel));
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

    private async Task RequireObjectTypeAsync(
        string repositoryRoot,
        string commit,
        string repositoryPath,
        string expectedType,
        string description,
        CancellationToken cancellationToken)
    {
        string actualType;
        try
        {
            actualType = await git.RunForTextAsync(
                repositoryRoot,
                ["cat-file", "-t", $"{commit}:{repositoryPath}"],
                cancellationToken);
        }
        catch (GitCommandException exception)
        {
            throw new RunPreparationException(
                $"Manca la {description} '{repositoryPath}' nel commit {commit}.",
                exception);
        }

        if (actualType != expectedType)
        {
            throw new RunPreparationException(
                $"La {description} '{repositoryPath}' nel commit {commit} è di tipo '{actualType}' invece di '{expectedType}'.");
        }
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

    private static void RequireNotExisting(string path, string description)
    {
        if (Directory.Exists(path) || File.Exists(path))
        {
            throw new RunPreparationException(
                $"{description} '{path}' esiste già: ogni run richiede directory nuove.");
        }
    }

    private static void CreateNewDirectory(string path, List<string> createdDirectories)
    {
        RequireNotExisting(path, "La directory");
        Directory.CreateDirectory(path);
        createdDirectories.Add(path);
    }

    private async Task ExtractBaselineAsync(
        string repositoryRoot,
        string commit,
        ExtractionState extraction,
        CancellationToken cancellationToken)
    {
        try
        {
            // 'git archive' legge i blob del commit, quindi ignora albero di lavoro e
            // file non versionati. Applica però la conversione dei fine riga della
            // configurazione host (Git for Windows imposta core.autocrlf=true a livello
            // di sistema): la si neutralizza perché l'area contenga i byte esatti dei
            // blob, uguali su qualsiasi macchina.
            await git.RunWithOutputStreamAsync(
                repositoryRoot,
                [
                    "-c", "core.autocrlf=false",
                    "-c", "core.eol=lf",
                    "archive", "--format=tar", $"{commit}:{extraction.SourceDirectory}",
                ],
                (archive, token) => ExtractArchiveAsync(archive, extraction, token),
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

    private static async Task ExtractArchiveAsync(Stream archive, ExtractionState extraction, CancellationToken cancellationToken)
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
                    // Le directory vengono create con i file che contengono; il percorso
                    // va comunque autorizzato, per rifiutare per esempio '.claude/'.
                    WorkspacePathPolicy.NormalizeAuthorizedPath(entry.Name);
                    continue;
                case TarEntryType.RegularFile or TarEntryType.V7RegularFile:
                    await ExtractFileAsync(entry, extraction, cancellationToken);
                    continue;
                default:
                    throw new RunPreparationException(
                        $"La baseline contiene una voce di tipo {entry.EntryType} non ammessa (sono consentiti solo file e directory): '{entry.Name}'.");
            }
        }
    }

    private static async Task ExtractFileAsync(TarEntry entry, ExtractionState extraction, CancellationToken cancellationToken)
    {
        string sourceRelativePath = WorkspacePathPolicy.NormalizeAuthorizedPath(entry.Name);
        string repositoryPath = $"{extraction.SourceDirectory}/{sourceRelativePath}";
        MaterialRoute route = TaskMaterialSelection.Route(sourceRelativePath, extraction.TaskId);

        string areaDirectory;
        switch (route.Destination)
        {
            case MaterialDestination.Excluded:
                extraction.ExcludedSourcePaths.Add(repositoryPath);
                return;
            case MaterialDestination.Workspace:
                areaDirectory = WorkspaceDirectoryName;
                extraction.WorkspaceFileCount++;
                break;
            default:
                areaDirectory = PublicReferenceDirectoryName;
                break;
        }

        string agentRootRelativePath = $"{areaDirectory}/{route.RelativePath}";
        string destination = ResolveDestination(extraction.AgentRoot, agentRootRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            if (entry.DataStream is not null)
            {
                await entry.DataStream.CopyToAsync(output, cancellationToken);
            }
        }

        extraction.SourcePaths[agentRootRelativePath] = repositoryPath;
    }

    private static string ResolveDestination(string agentRoot, string agentRootRelativePath)
    {
        string destination = Path.GetFullPath(
            Path.Combine(agentRoot, agentRootRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        // Controllo difensivo indipendente dalla politica: la destinazione deve restare in agent-root.
        if (!destination.StartsWith(agentRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new RunPreparationException($"La voce '{agentRootRelativePath}' risolve fuori dall'area agente.");
        }

        return destination;
    }

    private async Task WriteTaskSpecificationAsync(
        string repositoryRoot,
        string commit,
        string specificationSource,
        string agentRoot,
        CancellationToken cancellationToken)
    {
        string destination = Path.Combine(agentRoot, TaskSpecificationFileName);
        try
        {
            // 'cat-file blob' restituisce i byte del blob senza filtri né conversioni
            // dei fine riga: il prompt comune del task è identico per ogni architettura.
            await git.RunWithOutputStreamAsync(
                repositoryRoot,
                ["cat-file", "blob", $"{commit}:{specificationSource}"],
                async (content, token) =>
                {
                    await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await content.CopyToAsync(output, token);
                },
                cancellationToken);
        }
        catch (GitCommandException exception)
        {
            throw new RunPreparationException($"Lettura della specifica del task non riuscita. {exception.Message}", exception);
        }
    }

    private static void RemoveCreatedDirectories(List<string> createdDirectories, Exception originalException)
    {
        var failures = new List<string>();
        foreach (string directory in createdDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
            {
                failures.Add($"'{directory}': {cleanupException.Message}");
            }
        }

        if (failures.Count > 0)
        {
            throw new RunPreparationException(
                $"Preparazione fallita ({originalException.Message}) e rimozione delle directory incomplete non riuscita: {string.Join("; ", failures)}. Le directory non devono essere usate.",
                originalException);
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,63}\z")]
    private static partial Regex RunIdPattern();

    /// <summary>
    /// Stato accumulato durante l'estrazione di un archivio.
    /// </summary>
    private sealed class ExtractionState(string agentRoot, string sourceDirectory, string taskId)
    {
        public string AgentRoot { get; } = agentRoot;

        public string SourceDirectory { get; } = sourceDirectory;

        public string TaskId { get; } = taskId;

        /// <summary>Origine nel repository, per percorso relativo ad <c>agent-root</c>.</summary>
        public Dictionary<string, string> SourcePaths { get; } = new(StringComparer.Ordinal);

        /// <summary>File della baseline non consegnati, relativi alla radice del repository.</summary>
        public List<string> ExcludedSourcePaths { get; } = [];

        public int WorkspaceFileCount { get; set; }
    }
}
