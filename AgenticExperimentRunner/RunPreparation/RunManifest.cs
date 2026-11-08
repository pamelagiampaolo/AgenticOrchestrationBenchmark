using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Un file consegnato all'agente nell'area <c>agent-root</c>.
/// </summary>
/// <param name="Path">Percorso relativo ad <c>agent-root</c>, con separatore <c>/</c>.</param>
/// <param name="SourcePath">Percorso del blob di origine, relativo alla radice del repository.</param>
/// <param name="SizeBytes">Dimensione in byte.</param>
/// <param name="Sha256">SHA-256 del contenuto in esadecimale minuscolo.</param>
public sealed record RunManifestFile(string Path, string SourcePath, long SizeBytes, string Sha256);

/// <summary>
/// Provenienza e contenuto iniziale dell'area agente di una run, conservati nella
/// root dei risultati e quindi fuori dalla portata dell'agente.
/// </summary>
/// <param name="SchemaVersion">Versione del formato del manifest.</param>
/// <param name="RunId">Identificatore della run.</param>
/// <param name="TaskId">Task selezionato (<c>T1</c>, <c>T2</c>, ...).</param>
/// <param name="PreparedAtUtc">Istante UTC di completamento della preparazione.</param>
/// <param name="SourceCommit">SHA completo del commit da cui provengono tutti i file.</param>
/// <param name="SourceDirectory">Sottodirectory del repository usata come baseline.</param>
/// <param name="TaskSpecificationSource">Percorso della specifica nel repository.</param>
/// <param name="WritableDirectories">Directory di <c>agent-root</c> in cui l'agente può scrivere.</param>
/// <param name="FileCount">Numero di file consegnati.</param>
/// <param name="AggregateSha256">
/// SHA-256 del testo UTF-8 composto da una riga <c>&lt;sha256&gt;  &lt;percorso&gt;\n</c>
/// per file, nell'ordine di <paramref name="Files"/>. Dipende soltanto dal contenuto.
/// </param>
/// <param name="Files">File consegnati, ordinati per percorso con confronto ordinale.</param>
/// <param name="ExcludedSourcePaths">
/// File della baseline volutamente non consegnati (test pubblici di altri task),
/// relativi alla radice del repository e ordinati.
/// </param>
public sealed record RunManifest(
    int SchemaVersion,
    string RunId,
    string TaskId,
    DateTimeOffset PreparedAtUtc,
    string SourceCommit,
    string SourceDirectory,
    string TaskSpecificationSource,
    IReadOnlyList<string> WritableDirectories,
    int FileCount,
    string AggregateSha256,
    IReadOnlyList<RunManifestFile> Files,
    IReadOnlyList<string> ExcludedSourcePaths)
{
    /// <summary>
    /// Versione corrente dello schema. La 1 era il manifest A1 della sola workspace.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Descrive con dimensione e hash tutti i file presenti in <c>agent-root</c>.
    /// </summary>
    /// <param name="agentRoot">Percorso assoluto di <c>agent-root</c>.</param>
    /// <param name="sourcePaths">Origine nel repository di ogni file, per percorso relativo ad <c>agent-root</c>.</param>
    /// <returns>I file ordinati per percorso.</returns>
    /// <exception cref="RunPreparationException">Un file su disco non ha un'origine nota.</exception>
    /// <remarks>
    /// Gli hash sono calcolati sui file effettivamente presenti, così il manifest
    /// descrive ciò che l'agente riceve e non ciò che si intendeva copiare.
    /// </remarks>
    public static IReadOnlyList<RunManifestFile> DescribeAgentRoot(
        string agentRoot,
        IReadOnlyDictionary<string, string> sourcePaths)
    {
        return EnumerateRelativeFiles(agentRoot)
            .Select(relativePath => DescribeFile(agentRoot, relativePath, sourcePaths))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Elenca i file presenti sotto una directory come percorsi relativi con separatore <c>/</c>.
    /// </summary>
    /// <param name="root">Percorso assoluto della directory.</param>
    /// <returns>Percorsi relativi, in ordine non specificato.</returns>
    public static IEnumerable<string> EnumerateRelativeFiles(string root) =>
        Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(filePath => Path.GetRelativePath(root, filePath).Replace(Path.DirectorySeparatorChar, '/'));

    /// <summary>
    /// Calcola lo SHA-256 di un file.
    /// </summary>
    /// <param name="filePath">Percorso assoluto del file.</param>
    /// <returns>Hash esadecimale minuscolo e dimensione in byte.</returns>
    public static (string Sha256, long SizeBytes) HashFile(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        return (Convert.ToHexStringLower(SHA256.HashData(stream)), stream.Length);
    }

    /// <summary>
    /// Calcola l'hash aggregato di un elenco di file già ordinato.
    /// </summary>
    /// <param name="files">File nell'ordine in cui devono contribuire all'hash.</param>
    /// <returns>SHA-256 esadecimale minuscolo.</returns>
    public static string ComputeAggregateSha256(IEnumerable<RunManifestFile> files)
    {
        var builder = new StringBuilder();
        foreach (RunManifestFile file in files)
        {
            builder.Append(file.Sha256).Append("  ").Append(file.Path).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>
    /// Scrive il manifest come JSON UTF-8 senza BOM. Il file non deve già esistere.
    /// </summary>
    /// <param name="manifestPath">Percorso assoluto del file da creare.</param>
    /// <exception cref="IOException">Il file esiste già o non può essere scritto.</exception>
    public void WriteTo(string manifestPath)
    {
        using var stream = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, this, SerializerOptions);
    }

    /// <summary>
    /// Legge un manifest scritto da <see cref="WriteTo"/>.
    /// </summary>
    /// <param name="manifestPath">Percorso assoluto del manifest.</param>
    /// <returns>Il manifest letto.</returns>
    /// <exception cref="JsonException">Il contenuto non è un manifest valido o ha uno schema diverso.</exception>
    public static RunManifest ReadFrom(string manifestPath)
    {
        using FileStream stream = File.OpenRead(manifestPath);
        RunManifest manifest = JsonSerializer.Deserialize<RunManifest>(stream, SerializerOptions)
            ?? throw new JsonException($"Il manifest '{manifestPath}' è vuoto.");
        if (manifest.SchemaVersion != CurrentSchemaVersion)
        {
            throw new JsonException(
                $"Il manifest '{manifestPath}' ha schema {manifest.SchemaVersion}; è supportato soltanto lo schema {CurrentSchemaVersion}.");
        }

        return manifest;
    }

    private static RunManifestFile DescribeFile(
        string agentRoot,
        string relativePath,
        IReadOnlyDictionary<string, string> sourcePaths)
    {
        if (!sourcePaths.TryGetValue(relativePath, out string? sourcePath))
        {
            throw new RunPreparationException(
                $"Il file '{relativePath}' è presente nell'area agente ma non proviene dalla baseline.");
        }

        (string sha256, long sizeBytes) = HashFile(
            Path.Combine(agentRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return new RunManifestFile(relativePath, sourcePath, sizeBytes, sha256);
    }
}
