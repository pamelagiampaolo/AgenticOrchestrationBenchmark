namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Controlla che le directory che contengono l'area di run non forniscano alla CLI
/// dell'agente istruzioni, configurazioni o un repository Git.
/// </summary>
/// <remarks>
/// La CLI cerca file di istruzioni e configurazioni di progetto risalendo dalla
/// propria directory di lavoro (<c>session/</c>). Un file di questo tipo in una
/// directory superiore cambierebbe le condizioni iniziali della run senza comparire
/// nel manifest. La configurazione utente (<c>.claude</c> nel profilo) è esclusa
/// perché viene verificata dal launcher.
/// </remarks>
public static class SessionAncestorPolicy
{
    private static readonly string[] ConflictingNames =
    [
        "CLAUDE.md",
        "CLAUDE.local.md",
        ".mcp.json",
        ".claude",
        ".git",
    ];

    private const string UserConfigurationDirectoryName = ".claude";

    /// <summary>
    /// Cerca voci in conflitto in <paramref name="directory"/> e in tutte le sue
    /// directory superiori, fino alla radice del volume.
    /// </summary>
    /// <param name="directory">Percorso assoluto da cui iniziare (incluso).</param>
    /// <param name="userProfileDirectory">
    /// Profilo utente, in cui la sola directory <c>.claude</c> è ammessa; <see langword="null"/>
    /// se non va fatta alcuna eccezione.
    /// </param>
    /// <returns>Percorsi assoluti delle voci in conflitto, dalla più vicina alla più lontana.</returns>
    public static IReadOnlyList<string> FindConflicts(string directory, string? userProfileDirectory)
    {
        string? normalizedProfile = userProfileDirectory is null
            ? null
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(userProfileDirectory));
        var conflicts = new List<string>();

        for (DirectoryInfo? current = new(Path.GetFullPath(directory)); current is not null; current = current.Parent)
        {
            bool isUserProfile = normalizedProfile is not null
                && string.Equals(
                    Path.TrimEndingDirectorySeparator(current.FullName),
                    normalizedProfile,
                    StringComparison.OrdinalIgnoreCase);

            foreach (string name in ConflictingNames)
            {
                if (isUserProfile && name == UserConfigurationDirectoryName)
                {
                    continue;
                }

                string candidate = Path.Combine(current.FullName, name);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                {
                    conflicts.Add(candidate);
                }
            }
        }

        return conflicts;
    }
}
