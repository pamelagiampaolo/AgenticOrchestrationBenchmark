namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Parte dell'area agente da confrontare con il manifest.
/// </summary>
public enum IntegrityScope
{
    /// <summary>
    /// Tutto <c>agent-root</c>: da usare prima di avviare l'agente.
    /// </summary>
    AllFiles,

    /// <summary>
    /// Solo ciò che è fuori dalle directory scrivibili (test pubblici, specifica):
    /// da usare a fine run per dimostrare che i materiali di riferimento sono intatti.
    /// </summary>
    ProtectedOnly,
}

/// <summary>
/// Confronta lo stato su disco di <c>agent-root</c> con il manifest della run.
/// </summary>
public static class RunIntegrity
{
    /// <summary>
    /// Elenca le differenze tra manifest e disco nell'ambito richiesto.
    /// </summary>
    /// <param name="manifest">Manifest scritto durante la preparazione.</param>
    /// <param name="agentRoot">Percorso assoluto di <c>agent-root</c>.</param>
    /// <param name="scope">Parte dell'area da verificare.</param>
    /// <returns>
    /// Descrizioni ordinate delle differenze (<c>mancante</c>, <c>modificato</c>,
    /// <c>aggiunto</c>); un elenco vuoto indica che l'ambito coincide con il manifest.
    /// </returns>
    /// <exception cref="RunPreparationException"><paramref name="agentRoot"/> non esiste.</exception>
    public static IReadOnlyList<string> FindDifferences(RunManifest manifest, string agentRoot, IntegrityScope scope)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!Directory.Exists(agentRoot))
        {
            throw new RunPreparationException($"L'area agente '{agentRoot}' non esiste.");
        }

        bool IsInScope(string path) =>
            scope == IntegrityScope.AllFiles || !IsInsideWritableDirectory(path, manifest.WritableDirectories);

        Dictionary<string, RunManifestFile> expected = manifest.Files
            .Where(file => IsInScope(file.Path))
            .ToDictionary(file => file.Path, StringComparer.Ordinal);
        HashSet<string> actual = RunManifest.EnumerateRelativeFiles(agentRoot)
            .Where(IsInScope)
            .ToHashSet(StringComparer.Ordinal);

        var differences = new List<string>();
        foreach ((string path, RunManifestFile file) in expected)
        {
            if (!actual.Contains(path))
            {
                differences.Add($"mancante: {path}");
                continue;
            }

            (string sha256, long sizeBytes) = RunManifest.HashFile(
                Path.Combine(agentRoot, path.Replace('/', Path.DirectorySeparatorChar)));
            if (sha256 != file.Sha256 || sizeBytes != file.SizeBytes)
            {
                differences.Add($"modificato: {path}");
            }
        }

        differences.AddRange(actual.Where(path => !expected.ContainsKey(path)).Select(path => $"aggiunto: {path}"));
        differences.Sort(StringComparer.Ordinal);
        return differences;
    }

    private static bool IsInsideWritableDirectory(string path, IReadOnlyList<string> writableDirectories) =>
        writableDirectories.Any(directory =>
            path.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase));
}
