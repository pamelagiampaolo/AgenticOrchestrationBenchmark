using System.Diagnostics;
using System.Text.Json;

namespace AgentWorkspaceMcpServer;

/// <summary>
/// Esegue Vitest con eseguibile, entry point, configurazione e directory fissati dal controllore.
/// Questa classe fornisce audit e limiti operativi, non un confine di sicurezza del processo.
/// </summary>
public sealed class ControlledPublicTestRunner
{
    private const int MaximumDiagnosticLength = 12_000;
    private readonly string nodePath;
    private readonly string vitestEntryPoint;
    private readonly string configPath;
    private readonly string workingDirectory;
    private readonly string auditPath;
    private readonly string operation;
    private readonly TimeSpan timeout;
    private readonly string? testNamePattern;

    /// <summary>
    /// Configura il comando pubblico senza accettare argomenti dal chiamante MCP.
    /// </summary>
    /// <param name="nodePath">Copia fidata e assoluta di Node.js.</param>
    /// <param name="vitestEntryPoint">Entry point Vitest fidato e assoluto.</param>
    /// <param name="configPath">Configurazione Vitest fidata e assoluta.</param>
    /// <param name="workingDirectory">Copia effimera del progetto composto.</param>
    /// <param name="auditPath">File JSONL persistente, esterno alla directory di lavoro.</param>
    /// <param name="timeout">Durata massima del comando.</param>
    public ControlledPublicTestRunner(
        string nodePath,
        string vitestEntryPoint,
        string configPath,
        string workingDirectory,
        string auditPath,
        TimeSpan timeout)
        : this(nodePath, vitestEntryPoint, configPath, workingDirectory, auditPath, timeout, "public-tests", null)
    {
    }

    internal ControlledPublicTestRunner(
        string nodePath,
        string vitestEntryPoint,
        string configPath,
        string workingDirectory,
        string auditPath,
        TimeSpan timeout,
        string operation,
        string? testNamePattern = null)
    {
        this.nodePath = RequireFile(nodePath, nameof(nodePath));
        this.vitestEntryPoint = RequireFile(vitestEntryPoint, nameof(vitestEntryPoint));
        this.configPath = RequireFile(configPath, nameof(configPath));
        this.workingDirectory = RequireDirectory(workingDirectory, nameof(workingDirectory));
        this.auditPath = RequireAbsolutePath(auditPath, nameof(auditPath));
        this.operation = string.IsNullOrWhiteSpace(operation)
            ? throw new ArgumentException("L'operazione deve essere valorizzata.", nameof(operation))
            : operation;
        this.testNamePattern = testNamePattern;
        if (IsWithin(this.vitestEntryPoint, this.workingDirectory)
            || IsWithin(this.configPath, this.workingDirectory))
        {
            throw new ArgumentException(
                "Entry point e configurazione devono essere esterni alla directory modificabile eseguita.");
        }

        if (IsWithin(this.auditPath, this.workingDirectory))
        {
            throw new ArgumentException("Il log di audit deve essere esterno alla directory eseguita.", nameof(auditPath));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Il timeout deve essere positivo.", nameof(timeout));
        }

        this.timeout = timeout;
    }

    /// <summary>
    /// Esegue la sola suite pubblica preconfigurata e registra richiesta ed esito.
    /// </summary>
    /// <returns>Esito strutturato con durata e diagnostica limitata.</returns>
    public async Task<ControlledCommandResult> RunAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Stopwatch stopwatch = Stopwatch.StartNew();
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = nodePath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add(vitestEntryPoint);
        process.StartInfo.ArgumentList.Add("run");
        process.StartInfo.ArgumentList.Add("--config");
        process.StartInfo.ArgumentList.Add(configPath);
        process.StartInfo.ArgumentList.Add("--reporter=json");
        if (testNamePattern is not null)
        {
            process.StartInfo.ArgumentList.Add("--testNamePattern");
            process.StartInfo.ArgumentList.Add(testNamePattern);
        }

        string status;
        int? exitCode = null;
        string standardOutput = string.Empty;
        string standardError = string.Empty;
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Avvio di Node.js non riuscito.");
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using CancellationTokenSource timeoutSource = new(timeout);
            using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);
            try
            {
                await process.WaitForExitAsync(linkedSource.Token);
                standardOutput = await outputTask;
                standardError = await errorTask;
                exitCode = process.ExitCode;
                status = exitCode == 0 ? "completed" : "failed";
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                standardOutput = await outputTask;
                standardError = await errorTask;
                exitCode = process.ExitCode;
                status = "timeout";
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            standardError = exception.Message;
            status = "infrastructure-error";
        }

        stopwatch.Stop();
        var result = new ControlledCommandResult(
            operation,
            status,
            exitCode,
            stopwatch.ElapsedMilliseconds,
            Limit(standardOutput),
            Limit(standardError),
            null);
        await AppendAuditAsync(startedAt, result, cancellationToken);
        return result;
    }

    private async Task AppendAuditAsync(
        DateTimeOffset startedAt,
        ControlledCommandResult result,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(auditPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string record = JsonSerializer.Serialize(new
        {
            timestampUtc = startedAt,
            operation = result.Operation,
            executable = nodePath,
            arguments = testNamePattern is null
                ? new[] { vitestEntryPoint, "run", "--config", configPath, "--reporter=json" }
                : new[] { vitestEntryPoint, "run", "--config", configPath, "--reporter=json", "--testNamePattern", testNamePattern },
            workingDirectory,
            result.Status,
            result.ExitCode,
            result.DurationMilliseconds
        });
        await File.AppendAllTextAsync(auditPath, record + Environment.NewLine, cancellationToken);
    }

    private static string RequireFile(string path, string parameterName)
    {
        string fullPath = RequireAbsolutePath(path, parameterName);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException("Il file configurato non esiste.", fullPath);
    }

    private static string RequireDirectory(string path, string parameterName)
    {
        string fullPath = RequireAbsolutePath(path, parameterName);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException($"La directory configurata non esiste: '{fullPath}'.");
    }

    private static string RequireAbsolutePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Il percorso deve essere assoluto.", parameterName);
        }

        return Path.GetFullPath(path);
    }

    private static bool IsWithin(string candidate, string directory)
    {
        string prefix = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string Limit(string value) => value.Length <= MaximumDiagnosticLength
        ? value
        : value[..MaximumDiagnosticLength];
}

/// <summary>
/// Esito osservato di un comando Node controllato.
/// </summary>
public sealed record ControlledCommandResult(
    string Operation,
    string Status,
    int? ExitCode,
    long DurationMilliseconds,
    string StandardOutput,
    string StandardError,
    PublicTestCounts? Tests);

/// <summary>
/// Conteggi non sensibili estratti dal report pubblico di Vitest.
/// </summary>
public sealed record PublicTestCounts(
    int Total,
    int Passed,
    int Failed,
    int Pending,
    int TotalSuites,
    int PassedSuites,
    int FailedSuites,
    int PendingSuites);
