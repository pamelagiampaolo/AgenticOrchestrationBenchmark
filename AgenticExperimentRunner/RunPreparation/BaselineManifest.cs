using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Un file presente nella workspace preparata.
/// </summary>
/// <param name="Path">Percorso relativo alla workspace, con separatore <c>/</c>.</param>
/// <param name="SizeBytes">Dimensione in byte.</param>
/// <param name="Sha256">SHA-256 del contenuto in esadecimale minuscolo.</param>
public sealed record BaselineManifestFile(string Path, long SizeBytes, string Sha256);

/// <summary>
/// Provenienza e contenuto della workspace di una run, conservati fuori dalla
/// workspace dell'agente.
/// </summary>
/// <param name="SchemaVersion">Versione del formato del manifest.</param>
/// <param name="RunId">Identificatore della run.</param>
/// <param name="PreparedAtUtc">Istante UTC di completamento della preparazione.</param>
/// <param name="BaselineCommit">SHA completo del commit della baseline.</param>
/// <param name="SourceDirectory">Sottodirectory del repository copiata nella workspace.</param>
/// <param name="FileCount">Numero di file nella workspace.</param>
/// <param name="AggregateSha256">
/// SHA-256 del testo UTF-8 composto da una riga <c>&lt;sha256&gt;  &lt;percorso&gt;\n</c>
/// per file, nell'ordine di <paramref name="Files"/>. Dipende soltanto dal contenuto.
/// </param>
/// <param name="Files">File ordinati per percorso con confronto ordinale.</param>
public sealed record BaselineManifest(
    int SchemaVersion,
    string RunId,
    DateTimeOffset PreparedAtUtc,
    string BaselineCommit,
    string SourceDirectory,
    int FileCount,
    string AggregateSha256,
    IReadOnlyList<BaselineManifestFile> Files)
{
    /// <summary>
    /// Versione corrente dello schema del manifest.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Costruisce il manifest leggendo e calcolando l'hash di tutti i file presenti
    /// nella workspace.
    /// </summary>
    /// <param name="workspacePath">Percorso assoluto della workspace.</param>
    /// <param name="runId">Identificatore della run.</param>
    /// <param name="preparedAtUtc">Istante UTC da registrare.</param>
    /// <param name="baselineCommit">SHA completo del commit della baseline.</param>
    /// <param name="sourceDirectory">Sottodirectory sorgente nel repository.</param>
    /// <returns>Il manifest della workspace.</returns>
    /// <remarks>
    /// Gli hash sono calcolati sui file effettivamente presenti, così il manifest
    /// descrive ciò che l'agente riceverà e non ciò che si intendeva copiare.
    /// </remarks>
    public static BaselineManifest CreateFromWorkspace(
        string workspacePath,
        string runId,
        DateTimeOffset preparedAtUtc,
        string baselineCommit,
        string sourceDirectory)
    {
        List<BaselineManifestFile> files = Directory
            .EnumerateFiles(workspacePath, "*", SearchOption.AllDirectories)
            .Select(filePath => DescribeFile(workspacePath, filePath))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToList();

        return new BaselineManifest(
            CurrentSchemaVersion,
            runId,
            preparedAtUtc.ToUniversalTime(),
            baselineCommit,
            sourceDirectory,
            files.Count,
            ComputeAggregateSha256(files),
            files);
    }

    /// <summary>
    /// Calcola l'hash aggregato di un elenco di file già ordinato.
    /// </summary>
    /// <param name="files">File nell'ordine in cui devono contribuire all'hash.</param>
    /// <returns>SHA-256 esadecimale minuscolo.</returns>
    public static string ComputeAggregateSha256(IEnumerable<BaselineManifestFile> files)
    {
        var builder = new StringBuilder();
        foreach (BaselineManifestFile file in files)
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
    /// <exception cref="JsonException">Il contenuto non è un manifest valido.</exception>
    public static BaselineManifest ReadFrom(string manifestPath)
    {
        using FileStream stream = File.OpenRead(manifestPath);
        return JsonSerializer.Deserialize<BaselineManifest>(stream, SerializerOptions)
            ?? throw new JsonException($"Il manifest '{manifestPath}' è vuoto.");
    }

    private static BaselineManifestFile DescribeFile(string workspacePath, string filePath)
    {
        string relativePath = Path.GetRelativePath(workspacePath, filePath)
            .Replace(Path.DirectorySeparatorChar, '/');

        using FileStream stream = File.OpenRead(filePath);
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(stream));
        return new BaselineManifestFile(relativePath, stream.Length, sha256);
    }
}
