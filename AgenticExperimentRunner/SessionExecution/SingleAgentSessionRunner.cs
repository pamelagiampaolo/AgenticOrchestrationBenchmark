using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgenticExperimentRunner.RunPreparation;

namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Parametri di una sessione single-agent.
/// </summary>
/// <param name="PreparedRunPath">Percorso assoluto di <c>prepared-run.json</c> scritto da <c>prepare-run</c>.</param>
/// <param name="ClaudePath">Eseguibile della CLI.</param>
/// <param name="McpServerPath">Eseguibile del server MCP.</param>
/// <param name="Model">Identificatore esatto del modello.</param>
/// <param name="TimeLimit">Limite di <c>agentTaskDurationMs</c>.</param>
/// <param name="TokenBudget">Budget di <c>totalTokens</c>.</param>
public sealed record SingleAgentSessionRequest(
    string PreparedRunPath,
    string ClaudePath,
    string McpServerPath,
    string Model,
    TimeSpan TimeLimit,
    long TokenBudget);

/// <summary>
/// Esegue una sessione single-agent sull'area preparata e applica dall'esterno i
/// limiti di B4, registrando tutto nella directory dei risultati della run.
/// </summary>
public sealed class SingleAgentSessionRunner
{
    /// <summary>Nome dell'architettura registrato nel record.</summary>
    public const string ArchitectureName = "single-agent";

    /// <summary>Record finale nella directory dei risultati.</summary>
    public const string SessionRecordFileName = "session-record.json";

    /// <summary>Stdout grezzo della CLI.</summary>
    public const string StreamFileName = "stream.jsonl";

    /// <summary>Stderr della CLI.</summary>
    public const string StandardErrorFileName = "stderr.txt";

    /// <summary>Impostazioni fidate.</summary>
    public const string SettingsFileName = "cli-settings.json";

    /// <summary>Configurazione MCP.</summary>
    public const string McpConfigurationFileName = "mcp-config.json";

    /// <summary>Testo aggiunto al system prompt.</summary>
    public const string SystemPromptAppendFileName = "system-prompt-append.md";

    /// <summary>Istruzione comune, nella directory delle istruzioni.</summary>
    public const string CommonInstructionsFileName = "common-environment.md";

    /// <summary>Istruzione single-agent, nella directory delle istruzioni.</summary>
    public const string ArchitectureInstructionsFileName = "single-agent.md";

    private static readonly TimeSpan VersionProbeTimeout = TimeSpan.FromSeconds(60);
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly TimeProvider timeProvider;
    private readonly string instructionsDirectory;

    /// <summary>
    /// Crea il runner.
    /// </summary>
    /// <param name="timeProvider">Orologio per gli istanti registrati e il cronometro.</param>
    /// <param name="instructionsDirectory">Directory con le istruzioni versionate.</param>
    public SingleAgentSessionRunner(TimeProvider timeProvider, string instructionsDirectory)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.instructionsDirectory = instructionsDirectory ?? throw new ArgumentNullException(nameof(instructionsDirectory));
    }

    /// <summary>
    /// Esegue la sessione e scrive il record.
    /// </summary>
    /// <param name="request">Parametri.</param>
    /// <param name="cancellationToken">Annullamento: termina il processo e propaga l'annullamento.</param>
    /// <returns>Il record scritto e il suo percorso.</returns>
    /// <exception cref="SessionExecutionException">
    /// Area non integra, record già presente, file mancanti o CLI non avviabile.
    /// </exception>
    public async Task<(SessionRecord Record, string RecordPath)> RunAsync(SingleAgentSessionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        PrepareRunOutput prepared = ReadPreparedRun(request.PreparedRunPath);
        RunManifest manifest = ReadManifest(prepared.ManifestPath);
        string recordPath = Path.Combine(prepared.ResultsDirectory, SessionRecordFileName);
        foreach (string outputFile in new[] { SessionRecordFileName, StreamFileName, StandardErrorFileName, SettingsFileName, McpConfigurationFileName, SystemPromptAppendFileName })
        {
            if (File.Exists(Path.Combine(prepared.ResultsDirectory, outputFile)))
            {
                throw new SessionExecutionException(
                    $"'{outputFile}' esiste già in '{prepared.ResultsDirectory}': ogni area ospita una sola sessione.");
            }
        }

        IReadOnlyList<string> initialDifferences = RunIntegrity.FindDifferences(manifest, prepared.AgentRoot, IntegrityScope.AllFiles);
        if (initialDifferences.Count > 0)
        {
            throw new SessionExecutionException(
                $"L'area agente non coincide con il manifest e la sessione non viene avviata: {string.Join("; ", initialDifferences)}.");
        }

        if (Directory.EnumerateFileSystemEntries(prepared.SessionDirectory).Any())
        {
            throw new SessionExecutionException($"La directory di sessione '{prepared.SessionDirectory}' non è vuota.");
        }

        SessionInputs inputs = WriteSessionInputs(request, prepared);
        string cliVersion = await ReadCliVersionAsync(request.ClaudePath, prepared.SessionDirectory, cancellationToken);

        ProcessOutcome outcome = await RunCliAsync(request, prepared, inputs, cancellationToken);

        IReadOnlyList<string> protectedDifferences =
            RunIntegrity.FindDifferences(manifest, prepared.AgentRoot, IntegrityScope.ProtectedOnly);
        SessionRecord record = BuildRecord(request, prepared, inputs, cliVersion, outcome, protectedDifferences);
        record.WriteTo(recordPath);
        return (record, recordPath);
    }

    private static void ValidateRequest(SingleAgentSessionRequest request)
    {
        foreach ((string path, string description) in new[]
        {
            (request.PreparedRunPath, "prepared-run.json"),
            (request.ClaudePath, "eseguibile della CLI"),
            (request.McpServerPath, "eseguibile del server MCP"),
        })
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))
            {
                throw new SessionExecutionException($"Il percorso di {description} deve essere assoluto ed esistente: '{path}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new SessionExecutionException("Il modello deve essere specificato.");
        }

        if (request.TimeLimit <= TimeSpan.Zero || request.TokenBudget <= 0)
        {
            throw new SessionExecutionException("Limite di tempo e budget di token devono essere positivi.");
        }
    }

    private static PrepareRunOutput ReadPreparedRun(string path)
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

    private static RunManifest ReadManifest(string path)
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

    private SessionInputs WriteSessionInputs(SingleAgentSessionRequest request, PrepareRunOutput prepared)
    {
        byte[] taskPrompt = File.ReadAllBytes(Path.Combine(prepared.AgentRoot, prepared.TaskSpecificationPath));
        string commonInstructions = ReadInstructions(CommonInstructionsFileName);
        string architectureInstructions = ReadInstructions(ArchitectureInstructionsFileName);
        string systemPromptAppend = commonInstructions.TrimEnd('\n') + "\n\n" + architectureInstructions;

        string settingsPath = Path.Combine(prepared.ResultsDirectory, SettingsFileName);
        string mcpConfigurationPath = Path.Combine(prepared.ResultsDirectory, McpConfigurationFileName);
        string systemPromptAppendPath = Path.Combine(prepared.ResultsDirectory, SystemPromptAppendFileName);
        string settings = ClaudeLaunchProfile.BuildSettingsJson();
        string mcpConfiguration = ClaudeLaunchProfile.BuildMcpConfigurationJson(
            request.McpServerPath, prepared.AgentRoot, prepared.WritableDirectories);
        WriteNewText(settingsPath, settings);
        WriteNewText(mcpConfigurationPath, mcpConfiguration);
        WriteNewText(systemPromptAppendPath, systemPromptAppend);

        var hashes = new SessionHashes(
            Sha256(taskPrompt),
            Sha256(Utf8WithoutBom.GetBytes(commonInstructions)),
            Sha256(Utf8WithoutBom.GetBytes(architectureInstructions)),
            Sha256(Utf8WithoutBom.GetBytes(systemPromptAppend)),
            Sha256(Utf8WithoutBom.GetBytes(settings)),
            Sha256(Utf8WithoutBom.GetBytes(mcpConfiguration)),
            Sha256(File.ReadAllBytes(request.McpServerPath)),
            File.Exists(Path.ChangeExtension(request.McpServerPath, ".dll"))
                ? Sha256(File.ReadAllBytes(Path.ChangeExtension(request.McpServerPath, ".dll")))
                : null);
        return new SessionInputs(taskPrompt, settingsPath, mcpConfigurationPath, systemPromptAppendPath, hashes);
    }

    private string ReadInstructions(string fileName)
    {
        string path = Path.Combine(instructionsDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new SessionExecutionException($"Istruzione mancante: '{path}'.");
        }

        // Normalizzati a LF: l'hash non deve dipendere dai fine riga del checkout.
        return File.ReadAllText(path, Utf8WithoutBom).Replace("\r\n", "\n");
    }

    private static void WriteNewText(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(Utf8WithoutBom.GetBytes(content));
    }

    private static async Task<string> ReadCliVersionAsync(string claudePath, string workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(claudePath)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--version");
        ChildProcessEnvironment.RemoveInheritedAgentVariables(startInfo.Environment);

        using Process process = StartProcess(startInfo);
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(VersionProbeTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Terminate(process);
            throw new SessionExecutionException($"'{claudePath} --version' non ha risposto entro {VersionProbeTimeout.TotalSeconds} secondi.");
        }

        if (process.ExitCode != 0)
        {
            throw new SessionExecutionException($"'{claudePath} --version' è terminato con codice {process.ExitCode}: {await error}");
        }

        return (await output).Trim();
    }

    private async Task<ProcessOutcome> RunCliAsync(
        SingleAgentSessionRequest request,
        PrepareRunOutput prepared,
        SessionInputs inputs,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(request.ClaudePath)
        {
            WorkingDirectory = prepared.SessionDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8WithoutBom,
            StandardOutputEncoding = Utf8WithoutBom,
            StandardErrorEncoding = Utf8WithoutBom,
        };
        foreach (string argument in ClaudeLaunchProfile.BuildArguments(
            request.Model, inputs.McpConfigurationPath, inputs.SettingsPath, inputs.SystemPromptAppendPath))
        {
            startInfo.ArgumentList.Add(argument);
        }

        IReadOnlyList<string> removedVariables = ChildProcessEnvironment.RemoveInheritedAgentVariables(startInfo.Environment);
        var tracker = new StreamUsageTracker();
        var violations = new List<string>();
        string? terminationReason = null;
        bool timeExceeded = false;
        bool tokensExceeded = false;

        await using var streamFile = new StreamWriter(
            new FileStream(Path.Combine(prepared.ResultsDirectory, StreamFileName), FileMode.CreateNew, FileAccess.Write, FileShare.Read),
            Utf8WithoutBom);

        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        long startTimestamp = timeProvider.GetTimestamp();
        using Process process = StartProcess(startInfo);
        Task<string> standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

        // Il prompt viaggia su stdin: niente limiti di lunghezza o quoting della riga di comando.
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(inputs.TaskPrompt, cancellationToken);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // La CLI è già uscita: l'esito emerge da exit code e stream.
        }

        using var deadline = new CancellationTokenSource();
        Task deadlineTask = Task.Delay(request.TimeLimit, timeProvider, deadline.Token);
        Task cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
        try
        {
            Task<string?> pendingRead = process.StandardOutput.ReadLineAsync(CancellationToken.None).AsTask();
            while (true)
            {
                Task completed = await Task.WhenAny(pendingRead, deadlineTask, cancellationTask);
                if (completed == cancellationTask)
                {
                    Terminate(process);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (completed == deadlineTask)
                {
                    timeExceeded = true;
                    terminationReason = $"limite di tempo di {request.TimeLimit.TotalSeconds} secondi raggiunto";
                    Terminate(process);
                    break;
                }

                string? line = await pendingRead;
                if (line is null)
                {
                    break;
                }

                await streamFile.WriteAsync(line + "\n");
                if (!string.IsNullOrWhiteSpace(line)
                    && tracker.Observe(line) == StreamLineKind.Initialization
                    && violations.Count == 0)
                {
                    violations.AddRange(ClaudeLaunchProfile.FindConfigurationViolations(tracker.Initialization!, request.Model));
                    if (violations.Count > 0)
                    {
                        terminationReason = "configurazione effettiva diversa da quella prevista";
                        Terminate(process);
                        break;
                    }
                }

                if (tracker.ObservedUsage.TotalTokens >= request.TokenBudget)
                {
                    tokensExceeded = true;
                    timeExceeded = ElapsedSince(startTimestamp) >= request.TimeLimit;
                    terminationReason = $"budget di {request.TokenBudget} token raggiunto";
                    Terminate(process);
                    break;
                }

                pendingRead = process.StandardOutput.ReadLineAsync(CancellationToken.None).AsTask();
            }

            // Dopo la fine dello stdout il processo deve uscire entro il tempo residuo.
            if (terminationReason is null)
            {
                Task exit = process.WaitForExitAsync(CancellationToken.None);
                if (await Task.WhenAny(exit, deadlineTask) == deadlineTask)
                {
                    timeExceeded = true;
                    terminationReason = $"limite di tempo di {request.TimeLimit.TotalSeconds} secondi raggiunto";
                    Terminate(process);
                }
            }
        }
        finally
        {
            deadline.Cancel();
        }

        await process.WaitForExitAsync(CancellationToken.None);
        TimeSpan duration = ElapsedSince(startTimestamp);
        DateTimeOffset endedAt = timeProvider.GetUtcNow();
        string errorText = await standardError;
        await File.WriteAllTextAsync(
            Path.Combine(prepared.ResultsDirectory, StandardErrorFileName), errorText, Utf8WithoutBom, CancellationToken.None);

        return new ProcessOutcome(
            tracker,
            violations,
            terminationReason,
            timeExceeded,
            tokensExceeded,
            process.ExitCode,
            startedAt,
            endedAt,
            duration,
            removedVariables);
    }

    private TimeSpan ElapsedSince(long startTimestamp) => timeProvider.GetElapsedTime(startTimestamp);

    private static Process StartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new SessionExecutionException($"Avvio di '{startInfo.FileName}' non riuscito.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new SessionExecutionException($"Avvio di '{startInfo.FileName}' non riuscito: {exception.Message}", exception);
        }
    }

    private static void Terminate(Process process)
    {
        try
        {
            // L'albero comprende il server MCP avviato dalla CLI.
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Il processo è già terminato.
        }
    }

    private static SessionRecord BuildRecord(
        SingleAgentSessionRequest request,
        PrepareRunOutput prepared,
        SessionInputs inputs,
        string cliVersion,
        ProcessOutcome outcome,
        IReadOnlyList<string> protectedDifferences)
    {
        StreamUsageTracker tracker = outcome.Tracker;
        TokenUsage streamUsage = tracker.ObservedUsage;
        TokenUsage? authoritative = tracker.Result?.Usage;
        long tokensObserved = (authoritative ?? streamUsage).TotalTokens;
        SessionStatus status = DetermineStatus(outcome, tracker.Result, tokensObserved, request.TokenBudget);
        bool overBudget = status is SessionStatus.TokenLimitExceeded or SessionStatus.ResourceLimitsExceeded;

        return new SessionRecord(
            SessionRecord.CurrentSchemaVersion,
            prepared.RunId,
            prepared.TaskId,
            ArchitectureName,
            prepared.SourceCommit,
            status,
            outcome.StartedAt,
            outcome.EndedAt,
            (long)outcome.Duration.TotalMilliseconds,
            (long)request.TimeLimit.TotalMilliseconds,
            request.TokenBudget,
            tokensObserved,
            overBudget ? tokensObserved - request.TokenBudget : null,
            authoritative is not null,
            authoritative,
            streamUsage,
            tracker.Messages.Count > 0 && tracker.Messages.All(message => message.OutputIsFinal),
            authoritative is null ? null : authoritative == streamUsage,
            outcome.TerminationReason is not null,
            outcome.TerminationReason,
            outcome.ExitCode,
            request.Model,
            cliVersion,
            tracker.Initialization,
            outcome.Violations,
            tracker.Result,
            tracker.Messages,
            tracker.ToolCalls,
            tracker.ToolErrorCount,
            tracker.LineCount,
            tracker.MalformedLineCount,
            inputs.Hashes,
            request.ClaudePath,
            request.McpServerPath,
            outcome.RemovedEnvironmentVariables,
            protectedDifferences);
    }

    private static SessionStatus DetermineStatus(ProcessOutcome outcome, SessionResult? result, long tokensObserved, long tokenBudget)
    {
        if (outcome.Violations.Count > 0)
        {
            return SessionStatus.ConfigurationRejected;
        }

        bool tokensExceeded = outcome.TokensExceeded || tokensObserved > tokenBudget;
        if (outcome.TimeExceeded && tokensExceeded)
        {
            return SessionStatus.ResourceLimitsExceeded;
        }

        if (outcome.TimeExceeded)
        {
            return SessionStatus.TimeLimitExceeded;
        }

        if (tokensExceeded)
        {
            // Anche a fine naturale: l'output definitivo è noto solo dal result (B4).
            return SessionStatus.TokenLimitExceeded;
        }

        if (result is null)
        {
            return outcome.ExitCode == 0 ? SessionStatus.UsageMeasurementError : SessionStatus.CliFailed;
        }

        return outcome.ExitCode == 0 && result.Subtype == "success" && result.IsError == false
            ? SessionStatus.Completed
            : SessionStatus.CliFailed;
    }

    private static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private sealed record SessionInputs(
        byte[] TaskPrompt,
        string SettingsPath,
        string McpConfigurationPath,
        string SystemPromptAppendPath,
        SessionHashes Hashes);

    private sealed record ProcessOutcome(
        StreamUsageTracker Tracker,
        IReadOnlyList<string> Violations,
        string? TerminationReason,
        bool TimeExceeded,
        bool TokensExceeded,
        int ExitCode,
        DateTimeOffset StartedAt,
        DateTimeOffset EndedAt,
        TimeSpan Duration,
        IReadOnlyList<string> RemovedEnvironmentVariables);
}
