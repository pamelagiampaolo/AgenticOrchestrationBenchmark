using System.Diagnostics;
using System.Security.Cryptography;

/// <summary>
/// Esegue Git nei test in modo indipendente dal codice sotto verifica.
/// </summary>
internal static class GitTestProcess
{
    /// <summary>
    /// Esegue Git e restituisce lo standard output binario; fallisce se l'exit code è diverso da zero.
    /// </summary>
    public static byte[] Run(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} exit {process.ExitCode}: {standardError.Result}");
        }

        return output.ToArray();
    }

    /// <summary>
    /// Esegue Git e restituisce lo standard output come testo senza spazi finali.
    /// </summary>
    public static string RunForText(string workingDirectory, params string[] arguments) =>
        System.Text.Encoding.UTF8.GetString(Run(workingDirectory, arguments)).TrimEnd();

    /// <summary>
    /// SHA-256 esadecimale minuscolo di un contenuto.
    /// </summary>
    public static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}

/// <summary>
/// Repository Git usa e getta sotto %TEMP%, con configurazione locale esplicita.
/// </summary>
/// <remarks>
/// La firma dei commit è disattivata solo in questo repository di prova, perché la
/// configurazione globale dell'utente la richiede e i commit di fixture non devono
/// dipendere da chiavi personali.
/// </remarks>
internal sealed class TemporaryGitRepository : IDisposable
{
    public TemporaryGitRepository()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"a1-repo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
        Git("init", "--quiet", "--initial-branch=main");
        Git("config", "user.name", "A1 Test");
        Git("config", "user.email", "a1-test@example.invalid");
        Git("config", "commit.gpgsign", "false");
        Git("config", "core.autocrlf", "false");
        Git("config", "core.symlinks", "false");
    }

    /// <summary>Percorso assoluto della radice del repository.</summary>
    public string RootPath { get; }

    /// <summary>Scrive un file relativo alla radice (separatore '/').</summary>
    public void WriteFile(string relativePath, string content)
    {
        string path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>Aggiunge tutti i file e crea un commit; restituisce lo SHA completo.</summary>
    public string CommitAll(string message)
    {
        // --force: un eventuale gitignore globale non deve escludere i file vietati
        // che i test inseriscono di proposito.
        Git("add", "--all", "--force");
        Git("commit", "--quiet", "--message", message);
        return GitTestProcess.RunForText(RootPath, "rev-parse", "HEAD");
    }

    /// <summary>
    /// Registra nell'indice un collegamento simbolico senza crearlo sul disco,
    /// in modo che il commit contenga una voce di tipo symlink anche su Windows.
    /// </summary>
    public void StageSymbolicLink(string relativePath, string target)
    {
        string targetFile = Path.Combine(RootPath, $"symlink-target-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(targetFile, target);
        string blob = GitTestProcess.RunForText(RootPath, "hash-object", "-w", targetFile);
        File.Delete(targetFile);
        Git("update-index", "--add", "--cacheinfo", $"120000,{blob},{relativePath}");
    }

    /// <summary>Imposta un valore di configurazione locale del repository.</summary>
    public void SetConfig(string key, string value) => Git("config", key, value);

    /// <summary>Crea un commit con quanto già presente nell'indice.</summary>
    public string CommitIndex(string message)
    {
        Git("commit", "--quiet", "--message", message);
        return GitTestProcess.RunForText(RootPath, "rev-parse", "HEAD");
    }

    public void Dispose() => TestDirectories.DeleteRecursively(RootPath);

    private void Git(params string[] arguments) => GitTestProcess.Run(RootPath, arguments);
}

/// <summary>
/// Utilità per cartelle temporanee dei test.
/// </summary>
internal static class TestDirectories
{
    /// <summary>
    /// Trova l'eventuale profilo che contiene la temp directory reale del test.
    /// L'identità sandbox può differire dal proprietario del percorso restituito da <see cref="Path.GetTempPath"/>.
    /// </summary>
    public static string? TempUserProfile()
    {
        for (DirectoryInfo? directory = new(Path.GetTempPath()); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".claude")))
            {
                return directory.FullName;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>
    /// Elimina una directory anche se contiene file in sola lettura (come gli oggetti Git su Windows).
    /// </summary>
    public static void DeleteRecursively(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}

/// <summary>
/// Orologio fisso per rendere deterministico l'orario registrato nel manifest.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
