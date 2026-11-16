using System.Diagnostics;
using System.Text.Json;

namespace AgentWorkspaceMcpServer;

/// <summary>
/// Esegue l'unico runner fidato della suite pubblica e valida il report P2.2.
/// </summary>
public sealed class PublicSuiteRunner
{
    private readonly string runnerPath;
    private readonly TimeSpan timeout;

    /// <summary>
    /// Inizializza il collegamento verso uno script PowerShell fissato dal controllore.
    /// </summary>
    /// <param name="runnerPath">Percorso assoluto dello script P2.2 o di una fixture controllata.</param>
    /// <param name="timeout">Durata massima dell'intero processo runner.</param>
    /// <exception cref="ArgumentException">Il percorso non è assoluto o il timeout non è positivo.</exception>
    /// <exception cref="FileNotFoundException">Lo script configurato non esiste.</exception>
    public PublicSuiteRunner(string runnerPath, TimeSpan timeout)
    {
        if (!Path.IsPathFullyQualified(runnerPath))
        {
            throw new ArgumentException("Il runner deve avere un percorso assoluto.", nameof(runnerPath));
        }

        if (!File.Exists(runnerPath))
        {
            throw new FileNotFoundException("Il runner configurato non esiste.", runnerPath);
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Il timeout deve essere positivo.", nameof(timeout));
        }

        this.runnerPath = Path.GetFullPath(runnerPath);
        this.timeout = timeout;
    }

    /// <summary>
    /// Avvia il runner senza argomenti controllabili dal chiamante e restituisce il risultato validato.
    /// </summary>
    /// <returns>Versioni, commit, exit code e conteggi della suite pubblica.</returns>
    /// <exception cref="PublicSuiteRunException">Il runner fallisce, scade o non produce un report valido.</exception>
    public async Task<PublicSuiteResult> RunAsync()
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(runnerPath);

        if (!process.Start())
        {
            throw new PublicSuiteRunException("Avvio del runner P2.2 non riuscito.");
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeoutSource = new(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
                // Il processo può essere terminato tra il timeout e la richiesta di arresto.
            }

            throw new PublicSuiteRunException("Timeout del runner P2.2; nessun successo è stato registrato.");
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new PublicSuiteRunException(
                $"Il runner P2.2 è terminato con exit code {process.ExitCode}: {SafeDiagnostic(stderr)}");
        }

        string? evidenceDirectory = stdout
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .SingleOrDefault(line => line.StartsWith("HOST_EVIDENCE=", StringComparison.Ordinal))?
            ["HOST_EVIDENCE=".Length..];
        if (string.IsNullOrWhiteSpace(evidenceDirectory) || !Path.IsPathFullyQualified(evidenceDirectory))
        {
            throw new PublicSuiteRunException("Il runner P2.2 non ha dichiarato la directory delle evidenze host.");
        }

        string reportPath = Path.Combine(evidenceDirectory, "host-verification.json");
        if (!File.Exists(reportPath))
        {
            throw new PublicSuiteRunException("Il report host P2.2 è mancante.");
        }

        try
        {
            using JsonDocument report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath));
            JsonElement root = report.RootElement;
            JsonElement guest = root.GetProperty("guestResult");
            JsonElement checks = root.GetProperty("checks");
            PublicSuiteResult result = new(
                root.GetProperty("baselineSourceCommit").GetString() ?? string.Empty,
                root.GetProperty("nodeVersion").GetString() ?? string.Empty,
                root.GetProperty("npmVersion").GetString() ?? string.Empty,
                guest.GetProperty("build").GetProperty("exitCode").GetInt32(),
                guest.GetProperty("typecheck").GetProperty("exitCode").GetInt32(),
                guest.GetProperty("test").GetProperty("exitCode").GetInt32(),
                guest.GetProperty("totalTests").GetInt32(),
                guest.GetProperty("passedTests").GetInt32(),
                guest.GetProperty("failedTests").GetInt32(),
                guest.GetProperty("pendingTests").GetInt32(),
                checks.GetProperty("sandboxStopped").GetBoolean());

            if (!result.IsSuccessful)
            {
                throw new PublicSuiteRunException("Il report P2.2 non soddisfa l'esito pubblico atteso.");
            }

            return result;
        }
        catch (JsonException exception)
        {
            throw new PublicSuiteRunException("Il report host P2.2 non è JSON valido.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new PublicSuiteRunException("Il report host P2.2 contiene tipi non validi.", exception);
        }
        catch (KeyNotFoundException exception)
        {
            throw new PublicSuiteRunException("Il report host P2.2 è incompleto.", exception);
        }
    }

    private static string SafeDiagnostic(string diagnostic)
    {
        const int maximumLength = 500;
        string normalized = diagnostic.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }
}

/// <summary>
/// Risultato pubblico e non sensibile restituito dall'operazione MCP sperimentale.
/// </summary>
/// <param name="BaselineSourceCommit">Commit usato dal runner P2.2.</param>
/// <param name="NodeVersion">Versione Node.js osservata nel guest.</param>
/// <param name="NpmVersion">Versione npm osservata nel guest.</param>
/// <param name="BuildExitCode">Exit code della build.</param>
/// <param name="TypecheckExitCode">Exit code del typecheck.</param>
/// <param name="TestExitCode">Exit code della suite pubblica.</param>
/// <param name="TotalTests">Numero totale di test pubblici.</param>
/// <param name="PassedTests">Numero di test superati.</param>
/// <param name="FailedTests">Numero di test falliti.</param>
/// <param name="PendingTests">Numero di test pending o saltati.</param>
/// <param name="SandboxStopped">Indica che il runner non osservava istanze residue.</param>
public sealed record PublicSuiteResult(
    string BaselineSourceCommit,
    string NodeVersion,
    string NpmVersion,
    int BuildExitCode,
    int TypecheckExitCode,
    int TestExitCode,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int PendingTests,
    bool SandboxStopped)
{
    /// <summary>
    /// Indica che tutte le fasi e tutti i 27 test pubblici sono riusciti.
    /// </summary>
    public bool IsSuccessful => BuildExitCode == 0
        && TypecheckExitCode == 0
        && TestExitCode == 0
        && TotalTests == 27
        && PassedTests == 27
        && FailedTests == 0
        && PendingTests == 0
        && SandboxStopped;
}

/// <summary>
/// Indica che il runner non ha prodotto un risultato pubblico affidabile.
/// </summary>
public sealed class PublicSuiteRunException : Exception
{
    /// <summary>
    /// Inizializza un errore del runner.
    /// </summary>
    /// <param name="message">Descrizione diagnostica priva di risultati riservati.</param>
    public PublicSuiteRunException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Inizializza un errore del runner conservando la causa tecnica.
    /// </summary>
    /// <param name="message">Descrizione diagnostica priva di risultati riservati.</param>
    /// <param name="innerException">Errore tecnico originale.</param>
    public PublicSuiteRunException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
