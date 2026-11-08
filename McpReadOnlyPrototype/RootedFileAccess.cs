namespace McpReadOnlyPrototype;

/// <summary>
/// Fornisce accesso a file e directory contenuti in una root fissata.
/// </summary>
public sealed class RootedFileAccess
{
    private readonly string rootPath;
    private readonly string rootPrefix;
    private readonly IReadOnlyList<WritableRoot> writableRoots;

    /// <summary>
    /// Inizializza l'accesso confinato alla directory indicata.
    /// </summary>
    /// <param name="rootPath">Directory esistente che delimita tutti gli accessi consentiti.</param>
    /// <param name="writableDirectoryPaths">Directory relative esistenti nelle quali è consentita la scrittura.</param>
    /// <exception cref="ArgumentException">Il percorso non è assoluto.</exception>
    /// <exception cref="DirectoryNotFoundException">La directory non esiste.</exception>
    public RootedFileAccess(string rootPath, IEnumerable<string>? writableDirectoryPaths = null)
    {
        if (!Path.IsPathFullyQualified(rootPath))
        {
            throw new ArgumentException("La root deve essere un percorso assoluto.", nameof(rootPath));
        }

        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        if (!Directory.Exists(normalizedRoot))
        {
            throw new DirectoryNotFoundException($"La root non esiste: '{normalizedRoot}'.");
        }

        this.rootPath = ResolveExistingPath(normalizedRoot);
        rootPrefix = this.rootPath + Path.DirectorySeparatorChar;
        writableRoots = (writableDirectoryPaths ?? Array.Empty<string>())
            .Select(ResolveWritableRoot)
            .DistinctBy(root => root.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Elenca in ordine deterministico le voci immediate di una directory consentita.
    /// </summary>
    /// <param name="relativePath">Percorso relativo alla root; una stringa vuota indica la root.</param>
    /// <returns>Nomi relativi con suffisso <c>/</c> per le directory.</returns>
    /// <exception cref="AccessDeniedException">Il percorso è assoluto, attraversa la root o risolve fuori root.</exception>
    public IReadOnlyList<string> ListFiles(string relativePath)
    {
        string directoryPath = ResolveAllowedPath(relativePath, requireExisting: true);
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException("La directory richiesta non esiste.");
        }

        return Directory.EnumerateFileSystemEntries(directoryPath)
            .Select(entry => new
            {
                Name = Path.GetFileName(entry),
                IsDirectory = Directory.Exists(entry)
            })
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(entry => entry.IsDirectory ? entry.Name + "/" : entry.Name)
            .ToArray();
    }

    /// <summary>
    /// Legge un file UTF-8 contenuto nella root consentita.
    /// </summary>
    /// <param name="relativePath">Percorso relativo del file.</param>
    /// <returns>Contenuto testuale del file.</returns>
    /// <exception cref="AccessDeniedException">Il percorso è assoluto, attraversa la root o risolve fuori root.</exception>
    public string ReadFile(string relativePath)
    {
        string filePath = ResolveAllowedPath(relativePath, requireExisting: true);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Il file richiesto non esiste.");
        }

        return File.ReadAllText(filePath);
    }

    /// <summary>
    /// Crea o sostituisce un file UTF-8 contenuto nella root consentita.
    /// </summary>
    /// <param name="relativePath">Percorso relativo non vuoto del file.</param>
    /// <param name="content">Contenuto testuale completo da scrivere.</param>
    /// <exception cref="AccessDeniedException">Il percorso è assoluto, attraversa la root o risolve fuori root.</exception>
    /// <exception cref="IOException">Una componente del percorso esistente non è una directory.</exception>
    public void WriteFile(string relativePath, string content)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new AccessDeniedException("Accesso rifiutato: il percorso del file non può essere vuoto.");
        }

        if (Path.IsPathFullyQualified(relativePath))
        {
            throw new AccessDeniedException("Accesso rifiutato: sono ammessi soltanto percorsi relativi.");
        }

        string combinedPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        EnsureWithinRoot(combinedPath);
        EnsureWithinWritableRoot(combinedPath);
        string? requestedParent = Path.GetDirectoryName(combinedPath);
        if (string.IsNullOrWhiteSpace(requestedParent))
        {
            throw new AccessDeniedException("Accesso rifiutato: directory padre non valida.");
        }

        string resolvedParent = EnsureWritableDirectory(requestedParent);
        string destinationPath = Path.Combine(resolvedParent, Path.GetFileName(combinedPath));
        EnsureWithinRoot(destinationPath);
        EnsureWithinWritableRoot(destinationPath);

        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            string resolvedDestination = ResolveExistingPath(destinationPath);
            EnsureWithinRoot(resolvedDestination);
            EnsureWithinWritableRoot(resolvedDestination);
            if (Directory.Exists(resolvedDestination))
            {
                throw new IOException("Il percorso richiesto identifica una directory.");
            }

            destinationPath = resolvedDestination;
        }

        File.WriteAllText(destinationPath, content, new System.Text.UTF8Encoding(false));
    }

    private WritableRoot ResolveWritableRoot(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathFullyQualified(relativePath))
        {
            throw new ArgumentException(
                "Le directory scrivibili devono essere percorsi relativi non vuoti.",
                nameof(relativePath));
        }

        string combinedPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        EnsureWithinRoot(combinedPath);
        string resolvedPath = ResolveExistingPath(combinedPath);
        EnsureWithinRoot(resolvedPath);
        if (!Directory.Exists(resolvedPath))
        {
            throw new DirectoryNotFoundException("La directory scrivibile configurata non esiste.");
        }

        return new WritableRoot(resolvedPath, resolvedPath + Path.DirectorySeparatorChar);
    }

    private void EnsureWithinWritableRoot(string candidatePath)
    {
        string normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        bool isAllowed = writableRoots.Any(writableRoot =>
            normalizedCandidate.StartsWith(writableRoot.Prefix, StringComparison.OrdinalIgnoreCase));
        if (!isAllowed)
        {
            throw new AccessDeniedException("Accesso rifiutato: il percorso non appartiene a un'area scrivibile.");
        }
    }

    private string EnsureWritableDirectory(string requestedDirectory)
    {
        string relativeDirectory = Path.GetRelativePath(rootPath, requestedDirectory);
        string currentDirectory = rootPath;
        foreach (string segment in relativeDirectory.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries))
        {
            string nextPath = Path.Combine(currentDirectory, segment);
            if (File.Exists(nextPath))
            {
                throw new IOException("Una componente del percorso non è una directory.");
            }

            if (!Directory.Exists(nextPath))
            {
                Directory.CreateDirectory(nextPath);
            }

            currentDirectory = ResolveExistingPath(nextPath);
            EnsureWithinRoot(currentDirectory);
        }

        return currentDirectory;
    }

    private string ResolveAllowedPath(string requestedPath, bool requireExisting)
    {
        if (Path.IsPathFullyQualified(requestedPath))
        {
            throw new AccessDeniedException("Accesso rifiutato: sono ammessi soltanto percorsi relativi.");
        }

        string combinedPath = Path.GetFullPath(Path.Combine(rootPath, requestedPath));
        EnsureWithinRoot(combinedPath);

        string resolvedPath = requireExisting ? ResolveExistingPath(combinedPath) : combinedPath;
        EnsureWithinRoot(resolvedPath);
        return resolvedPath;
    }

    private void EnsureWithinRoot(string candidatePath)
    {
        string normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        if (!string.Equals(normalizedCandidate, rootPath, StringComparison.OrdinalIgnoreCase)
            && !normalizedCandidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new AccessDeniedException("Accesso rifiutato: il percorso risolve fuori dalla root consentita.");
        }
    }

    private static string ResolveExistingPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            throw new IOException("Impossibile determinare la root del percorso.");
        }

        string currentPath = root;
        foreach (string segment in fullPath[root.Length..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            string nextPath = Path.Combine(currentPath, segment);
            FileSystemInfo entry = Directory.Exists(nextPath)
                ? new DirectoryInfo(nextPath)
                : new FileInfo(nextPath);

            if (!entry.Exists)
            {
                throw new FileNotFoundException("Il percorso richiesto non esiste.");
            }

            FileSystemInfo? target = entry.ResolveLinkTarget(returnFinalTarget: true);
            currentPath = target?.FullName ?? entry.FullName;
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentPath));
    }

    private sealed record WritableRoot(string Path, string Prefix);
}

/// <summary>
/// Indica che una richiesta ha tentato di superare il confine della root consentita.
/// </summary>
public sealed class AccessDeniedException : Exception
{
    /// <summary>
    /// Inizializza l'errore con un messaggio che non include contenuti del file negato.
    /// </summary>
    /// <param name="message">Descrizione sicura del rifiuto.</param>
    public AccessDeniedException(string message)
        : base(message)
    {
    }
}
