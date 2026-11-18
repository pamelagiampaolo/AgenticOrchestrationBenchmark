namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Stabilisce quali percorsi della baseline possono essere copiati nella workspace
/// dell'agente. La politica è pura: non accede al file system.
/// </summary>
/// <remarks>
/// I percorsi vietati fanno fallire la preparazione invece di essere saltati, così
/// che una baseline contaminata (test riservati, credenziali, metadati Git) venga
/// scoperta e non produca silenziosamente run diverse da quelle approvate.
/// </remarks>
public static class WorkspacePathPolicy
{
    private const char Separator = '/';

    private static readonly string[] ForbiddenSegments =
    [
        ".git",
        "node_modules",
        ".gitconfig",
        ".npmrc",
        ".env",

        // Istruzioni e configurazioni della CLI dell'agente: se fossero nella baseline
        // altererebbero le condizioni iniziali in modo diverso tra le architetture (B4).
        ".claude",
        "CLAUDE.md",
        "CLAUDE.local.md",
        "AGENTS.md",
        ".mcp.json",
    ];

    private const string ForbiddenEnvironmentFilePrefix = ".env.";

    private const string ReservedTestsDirectory = "tests/reserved";

    /// <summary>
    /// Verifica un percorso relativo proveniente dall'archivio della baseline e lo
    /// restituisce in forma normalizzata, con separatore <c>/</c> e senza barra finale.
    /// </summary>
    /// <param name="archivePath">Percorso relativo letto dall'archivio, con separatore <c>/</c>.</param>
    /// <returns>Il percorso normalizzato e autorizzato.</returns>
    /// <exception cref="RunPreparationException">
    /// Il percorso è vuoto, assoluto, contiene segmenti di attraversamento o caratteri
    /// non ammessi, oppure identifica materiale non autorizzato.
    /// </exception>
    public static string NormalizeAuthorizedPath(string archivePath)
    {
        string path = (archivePath ?? string.Empty).TrimEnd(Separator);
        RequireWellFormedRelativePath(archivePath ?? string.Empty, path);

        foreach (string segment in path.Split(Separator))
        {
            if (IsForbiddenSegment(segment))
            {
                throw new RunPreparationException(
                    $"La baseline contiene materiale non autorizzato per la workspace dell'agente: '{path}'.");
            }
        }

        if (IsInsideReservedTests(path))
        {
            throw new RunPreparationException(
                $"La baseline contiene test riservati, che non devono mai raggiungere l'agente: '{path}'.");
        }

        return path;
    }

    private static void RequireWellFormedRelativePath(string originalPath, string path)
    {
        if (path.Length == 0)
        {
            throw new RunPreparationException("La baseline contiene una voce con percorso vuoto.");
        }

        // ':' esclude lettere di unità e alternate data stream di NTFS; '\' esclude
        // percorsi che su Windows verrebbero interpretati con un separatore diverso.
        if (path.StartsWith(Separator) || path.Contains('\\') || path.Contains(':'))
        {
            throw new RunPreparationException(
                $"La baseline contiene un percorso assoluto o con caratteri non ammessi: '{originalPath}'.");
        }

        foreach (string segment in path.Split(Separator))
        {
            if (segment is "" or "." or "..")
            {
                throw new RunPreparationException(
                    $"La baseline contiene un percorso con segmenti non ammessi: '{originalPath}'.");
            }
        }
    }

    private static bool IsForbiddenSegment(string segment) =>
        ForbiddenSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)
        || segment.StartsWith(ForbiddenEnvironmentFilePrefix, StringComparison.OrdinalIgnoreCase);

    private static bool IsInsideReservedTests(string path) =>
        string.Equals(path, ReservedTestsDirectory, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(ReservedTestsDirectory + Separator, StringComparison.OrdinalIgnoreCase);
}
