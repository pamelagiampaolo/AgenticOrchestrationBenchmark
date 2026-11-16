using System.Diagnostics;
using System.Text.Json;

namespace AgentWorkspaceMcpServer;

/// <summary>
/// Esegue TypeScript typecheck su una copia effimera della workspace con comando fisso.
/// Fornisce limiti e audit, ma non costituisce isolamento del processo host.
/// </summary>
public sealed class EphemeralTypecheckRunner
{
    private const int MaximumDiagnosticLength = 12_000;
    private readonly string workspaceSource;
    private readonly string workerRoot;
    private readonly string nodePath;
    private readonly string typeScriptEntryPoint;
    private readonly string auditPath;
    private readonly TimeSpan timeout;
    private readonly string operation;
    private readonly bool noEmit;

    /// <summary>
    /// Configura workspace, toolchain, audit e timeout senza input controllabili dal chiamante MCP.
    /// </summary>
    public EphemeralTypecheckRunner(
        string agentRoot,
        string workerRoot,
        string nodePath,
        string typeScriptEntryPoint,
        string auditPath,
        TimeSpan timeout)
        : this(agentRoot, workerRoot, nodePath, typeScriptEntryPoint, auditPath, timeout, "typecheck", noEmit: true)
    {
    }

    internal EphemeralTypecheckRunner(
        string agentRoot,
        string workerRoot,
        string nodePath,
        string typeScriptEntryPoint,
        string auditPath,
        TimeSpan timeout,
        string operation,
        bool noEmit)
    {
        string normalizedAgentRoot = RequireDirectory(agentRoot, nameof(agentRoot));
        workspaceSource = RequireDirectory(Path.Combine(normalizedAgentRoot, "workspace"), "workspace");
        this.workerRoot = RequireAbsolutePath(workerRoot, nameof(workerRoot));
        this.nodePath = RequireFile(nodePath, nameof(nodePath));
        this.typeScriptEntryPoint = RequireFile(typeScriptEntryPoint, nameof(typeScriptEntryPoint));
        this.auditPath = RequireAbsolutePath(auditPath, nameof(auditPath));
        this.timeout = timeout > TimeSpan.Zero
            ? timeout
            : throw new ArgumentException("Il timeout deve essere positivo.", nameof(timeout));
        this.operation = operation;
        this.noEmit = noEmit;
        if (IsWithin(this.workerRoot, normalizedAgentRoot) || IsWithin(this.auditPath, normalizedAgentRoot))
        {
            throw new ArgumentException("Worker e audit devono essere esterni alla root MCP dell'agente.");
        }
    }

    /// <summary>
    /// Copia la workspace, invoca <c>tsc --noEmit --project tsconfig.json</c>, registra l'esito e pulisce la copia.
    /// </summary>
    public async Task<ControlledCommandResult> RunAsync(CancellationToken cancellationToken = default)
    {
        string invocationRoot = Path.Combine(workerRoot, Guid.NewGuid().ToString("N"));
        string project = Path.Combine(invocationRoot, "project");
        Directory.CreateDirectory(project);
        try
        {
            CopyWorkspace(workspaceSource, project);
            string configPath = Path.Combine(project, "tsconfig.json");
            if (!File.Exists(configPath))
            {
                return await RecordInfrastructureErrorAsync(
                    invocationRoot,
                    "La workspace non contiene tsconfig.json.",
                    cancellationToken);
            }

            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            Stopwatch stopwatch = Stopwatch.StartNew();
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = nodePath,
                    WorkingDirectory = project,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.ArgumentList.Add(typeScriptEntryPoint);
            if (noEmit)
            {
                process.StartInfo.ArgumentList.Add("--noEmit");
            }
            process.StartInfo.ArgumentList.Add("--project");
            process.StartInfo.ArgumentList.Add(configPath);

            string status;
            int? exitCode = null;
            string output = string.Empty;
            string error = string.Empty;
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
                    output = await outputTask;
                    error = await errorTask;
                    exitCode = process.ExitCode;
                    status = exitCode == 0 ? "completed" : "failed";
                }
                catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                    output = await outputTask;
                    error = await errorTask;
                    exitCode = process.ExitCode;
                    status = "timeout";
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                error = exception.Message;
                status = "infrastructure-error";
            }

            stopwatch.Stop();
            var result = new ControlledCommandResult(
                operation,
                status,
                exitCode,
                stopwatch.ElapsedMilliseconds,
                SanitizeAndLimit(output, invocationRoot),
                SanitizeAndLimit(error, invocationRoot),
                null);
            await AppendAuditAsync(startedAt, project, result, cancellationToken);
            return result;
        }
        finally
        {
            if (Directory.Exists(invocationRoot))
            {
                Directory.Delete(invocationRoot, recursive: true);
            }
        }
    }

    private async Task<ControlledCommandResult> RecordInfrastructureErrorAsync(
        string invocationRoot,
        string message,
        CancellationToken cancellationToken)
    {
        var result = new ControlledCommandResult(operation, "infrastructure-error", null, 0, string.Empty, message, null);
        await AppendAuditAsync(DateTimeOffset.UtcNow, Path.Combine(invocationRoot, "project"), result, cancellationToken);
        return result;
    }

    private async Task AppendAuditAsync(
        DateTimeOffset startedAt,
        string workingDirectory,
        ControlledCommandResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(auditPath)!);
        var arguments = new List<string> { typeScriptEntryPoint };
        if (noEmit)
        {
            arguments.Add("--noEmit");
        }
        arguments.Add("--project");
        arguments.Add("<worker>/project/tsconfig.json");
        string record = JsonSerializer.Serialize(new
        {
            timestampUtc = startedAt,
            operation = result.Operation,
            executable = nodePath,
            arguments,
            workingDirectory,
            result.Status,
            result.ExitCode,
            result.DurationMilliseconds
        });
        await File.AppendAllTextAsync(auditPath, record + Environment.NewLine, cancellationToken);
    }

    private static void CopyWorkspace(string sourceRoot, string destinationRoot)
    {
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            RejectLink(directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            RejectLink(file);
            string destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: false);
        }
    }

    private string SanitizeAndLimit(string diagnostic, string invocationRoot)
    {
        foreach ((string path, string replacement) in new[]
        {
            (invocationRoot, "<worker>"),
            (Path.GetDirectoryName(typeScriptEntryPoint)!, "<toolchain>"),
            (nodePath, "<node>")
        }.OrderByDescending(item => item.Item1.Length))
        {
            diagnostic = diagnostic.Replace(path, replacement, StringComparison.OrdinalIgnoreCase);
        }

        return diagnostic.Length <= MaximumDiagnosticLength ? diagnostic : diagnostic[..MaximumDiagnosticLength];
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Collegamento non ammesso nella workspace: '{path}'.");
        }
    }

    private static string RequireFile(string path, string parameterName)
    {
        string fullPath = RequireAbsolutePath(path, parameterName);
        return File.Exists(fullPath) ? fullPath : throw new FileNotFoundException("Il file configurato non esiste.", fullPath);
    }

    private static string RequireDirectory(string path, string parameterName)
    {
        string fullPath = RequireAbsolutePath(path, parameterName);
        return Directory.Exists(fullPath) ? fullPath : throw new DirectoryNotFoundException($"Directory inesistente: '{fullPath}'.");
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
}
