using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgenticExperimentRunner.RunPreparation;

namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Lettura dei documenti di una run preparata e scrittura dei file di condizione
/// delle sessioni, comuni a tutte le architetture.
/// </summary>
public static class RunInputs
{
    /// <summary>Codifica di tutti i file di testo scritti dall'orchestratore.</summary>
    public static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Legge <c>prepared-run.json</c>.
    /// </summary>
    /// <param name="path">Percorso del documento.</param>
    /// <returns>Il documento.</returns>
    /// <exception cref="SessionExecutionException">Documento illeggibile o di schema non supportato.</exception>
    public static PrepareRunOutput ReadPreparedRun(string path)
    {
        try
        {
            PrepareRunOutput prepared = JsonSerializer.Deserialize<PrepareRunOutput>(
                File.ReadAllText(path), PrepareRunOutput.SerializerOptions)
                ?? throw new JsonException("documento vuoto");
            if (prepared.SchemaVersion != PrepareRunOutput.CurrentSchemaVersion)
            {
                throw new JsonException($"schema {prepared.SchemaVersion} non supportato");
            }

            return prepared;
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            throw new SessionExecutionException($"Lettura di '{path}' non riuscita: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Legge il manifest della run.
    /// </summary>
    /// <param name="path">Percorso del manifest.</param>
    /// <returns>Il manifest.</returns>
    /// <exception cref="SessionExecutionException">Manifest illeggibile.</exception>
    public static RunManifest ReadManifest(string path)
    {
        try
        {
            return RunManifest.ReadFrom(path);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            throw new SessionExecutionException($"Lettura del manifest '{path}' non riuscita: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Verifica che l'area agente coincida interamente con il manifest e che la
    /// directory di sessione sia vuota: una sessione parte solo da una copia pulita.
    /// </summary>
    /// <param name="prepared">Run preparata.</param>
    /// <param name="manifest">Manifest della run.</param>
    /// <exception cref="SessionExecutionException">Area alterata o sessione non vuota.</exception>
    public static void EnsurePristine(PrepareRunOutput prepared, RunManifest manifest)
    {
        IReadOnlyList<string> differences = RunIntegrity.FindDifferences(manifest, prepared.AgentRoot, IntegrityScope.AllFiles);
        if (differences.Count > 0)
        {
            throw new SessionExecutionException(
                $"L'area agente non coincide con il manifest e la sessione non viene avviata: {string.Join("; ", differences)}.");
        }

        if (Directory.EnumerateFileSystemEntries(prepared.SessionDirectory).Any())
        {
            throw new SessionExecutionException($"La directory di sessione '{prepared.SessionDirectory}' non è vuota.");
        }
    }

    /// <summary>
    /// Legge un'istruzione versionata normalizzando i fine riga a LF, così l'hash non
    /// dipende dal checkout.
    /// </summary>
    /// <param name="instructionsDirectory">Directory delle istruzioni.</param>
    /// <param name="fileName">Nome del file.</param>
    /// <returns>Il testo.</returns>
    /// <exception cref="SessionExecutionException">Istruzione mancante.</exception>
    public static string ReadInstructions(string instructionsDirectory, string fileName)
    {
        string path = Path.Combine(instructionsDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new SessionExecutionException($"Istruzione mancante: '{path}'.");
        }

        return File.ReadAllText(path, Utf8WithoutBom).Replace("\r\n", "\n");
    }

    /// <summary>
    /// Testo aggiunto al system prompt: istruzione comune seguita da quella del ruolo.
    /// </summary>
    /// <param name="commonInstructions">Istruzione comune.</param>
    /// <param name="roleInstructions">Istruzione dell'architettura o del ruolo.</param>
    /// <returns>Il testo combinato.</returns>
    public static string CombineInstructions(string commonInstructions, string roleInstructions) =>
        commonInstructions.TrimEnd('\n') + "\n\n" + roleInstructions;

    /// <summary>
    /// Scrive un file di testo UTF-8 senza BOM. Il file non deve esistere.
    /// </summary>
    /// <param name="path">Percorso.</param>
    /// <param name="content">Contenuto.</param>
    public static void WriteNewText(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(Utf8WithoutBom.GetBytes(content));
    }

    /// <summary>SHA-256 esadecimale minuscolo.</summary>
    /// <param name="content">Byte.</param>
    /// <returns>L'hash.</returns>
    public static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>SHA-256 della codifica UTF-8 senza BOM di un testo.</summary>
    /// <param name="text">Testo.</param>
    /// <returns>L'hash.</returns>
    public static string Sha256(string text) => Sha256(Utf8WithoutBom.GetBytes(text));

    /// <summary>
    /// SHA-256 dell'assembly <c>.dll</c> accanto a un eseguibile .NET, che è soltanto
    /// l'apphost: il codice è nell'assembly.
    /// </summary>
    /// <param name="executablePath">Eseguibile.</param>
    /// <returns>L'hash, oppure <see langword="null"/> se l'assembly non esiste.</returns>
    public static string? AssemblySha256(string executablePath)
    {
        string assemblyPath = Path.ChangeExtension(executablePath, ".dll");
        return File.Exists(assemblyPath) ? Sha256(File.ReadAllBytes(assemblyPath)) : null;
    }
}
