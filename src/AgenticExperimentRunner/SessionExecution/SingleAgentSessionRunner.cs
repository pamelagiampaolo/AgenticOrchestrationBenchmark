using AgenticExperimentRunner.RunPreparation;

namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Parametri di una sessione single-agent.
/// </summary>
/// <param name="PreparedRunPath">Percorso assoluto di <c>prepared-run.json</c> scritto da <c>prepare-run</c>.</param>
/// <param name="ClaudePath">Eseguibile della CLI.</param>
/// <param name="McpServerPath">Eseguibile del server MCP.</param>
/// <param name="McpToolchain">Toolchain fidata per build e test.</param>
/// <param name="Model">Identificatore esatto del modello.</param>
/// <param name="TimeLimit">Limite di <c>agentTaskDurationMs</c>.</param>
/// <param name="TokenBudget">Budget di <c>totalTokens</c>.</param>
/// <param name="ExperimentMetadata">Identità della run nel piano sperimentale.</param>
public sealed record SingleAgentSessionRequest(
    string PreparedRunPath,
    string ClaudePath,
    string McpServerPath,
    McpDevelopmentToolchain McpToolchain,
    string Model,
    TimeSpan TimeLimit,
    long TokenBudget,
    ExperimentRunMetadata ExperimentMetadata);

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
        PrepareRunOutput prepared = RunInputs.ReadPreparedRun(request.PreparedRunPath);
        RunManifest manifest = RunInputs.ReadManifest(prepared.ManifestPath);
        string recordPath = Path.Combine(prepared.ResultsDirectory, SessionRecordFileName);
        foreach (string outputFile in new[] { SessionRecordFileName, StreamFileName, StandardErrorFileName, SettingsFileName, McpConfigurationFileName, SystemPromptAppendFileName })
        {
            if (File.Exists(Path.Combine(prepared.ResultsDirectory, outputFile)))
            {
                throw new SessionExecutionException(
                    $"'{outputFile}' esiste già in '{prepared.ResultsDirectory}': ogni area ospita una sola sessione.");
            }
        }

        RunInputs.EnsurePristine(prepared, manifest);

        SessionInputs inputs = WriteSessionInputs(request, prepared);
        string cliVersion = await AgentProcess.ReadCliVersionAsync(request.ClaudePath, prepared.SessionDirectory, cancellationToken);
        ToolchainIdentity toolchain = await ToolchainIdentity.InspectAsync(request.McpToolchain, prepared.SessionDirectory, cancellationToken);

        ProcessOutcome outcome = await RunCliAsync(request, prepared, inputs, cancellationToken);

        IReadOnlyList<string> protectedDifferences =
            RunIntegrity.FindDifferences(manifest, prepared.AgentRoot, IntegrityScope.ProtectedOnly);
        SessionRecord record = BuildRecord(request, prepared, inputs, cliVersion, toolchain, outcome, protectedDifferences);
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
            (request.McpToolchain.NodePath, "eseguibile Node.js"),
            (request.McpToolchain.VitestPath, "entry point Vitest"),
            (request.McpToolchain.TypeScriptPath, "entry point TypeScript"),
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

        string? metadataError = request.ExperimentMetadata?.FindValidationError();
        if (metadataError is not null || request.ExperimentMetadata is null)
        {
            throw new SessionExecutionException(metadataError ?? "I metadati sperimentali devono essere specificati.");
        }
    }

    private SessionInputs WriteSessionInputs(SingleAgentSessionRequest request, PrepareRunOutput prepared)
    {
        byte[] taskPrompt = File.ReadAllBytes(Path.Combine(prepared.AgentRoot, prepared.TaskSpecificationPath));
        string commonInstructions = RunInputs.ReadInstructions(instructionsDirectory, CommonInstructionsFileName);
        string architectureInstructions = RunInputs.ReadInstructions(instructionsDirectory, ArchitectureInstructionsFileName);
        string systemPromptAppend = RunInputs.CombineInstructions(commonInstructions, architectureInstructions);

        string settingsPath = Path.Combine(prepared.ResultsDirectory, SettingsFileName);
        string mcpConfigurationPath = Path.Combine(prepared.ResultsDirectory, McpConfigurationFileName);
        string systemPromptAppendPath = Path.Combine(prepared.ResultsDirectory, SystemPromptAppendFileName);
        string settings = ClaudeLaunchProfile.BuildSettingsJson();
        string mcpConfiguration = ClaudeLaunchProfile.BuildMcpConfigurationJson(
            request.McpServerPath,
            prepared.AgentRoot,
            prepared.WritableDirectories,
            request.McpToolchain,
            Path.Combine(prepared.ResultsDirectory, "mcp-worker"),
            Path.Combine(prepared.ResultsDirectory, "mcp-audit"),
            prepared.TaskId);
        RunInputs.WriteNewText(settingsPath, settings);
        RunInputs.WriteNewText(mcpConfigurationPath, mcpConfiguration);
        RunInputs.WriteNewText(systemPromptAppendPath, systemPromptAppend);

        var hashes = new SessionHashes(
            RunInputs.Sha256(taskPrompt),
            RunInputs.Sha256(commonInstructions),
            RunInputs.Sha256(architectureInstructions),
            RunInputs.Sha256(systemPromptAppend),
            RunInputs.Sha256(settings),
            RunInputs.Sha256(mcpConfiguration),
            RunInputs.Sha256(File.ReadAllBytes(request.McpServerPath)),
            RunInputs.AssemblySha256(request.McpServerPath));
        return new SessionInputs(taskPrompt, settingsPath, mcpConfigurationPath, systemPromptAppendPath, hashes);
    }

    private async Task<ProcessOutcome> RunCliAsync(
        SingleAgentSessionRequest request,
        PrepareRunOutput prepared,
        SessionInputs inputs,
        CancellationToken cancellationToken)
    {
        var launch = new AgentLaunch(
            ArchitectureName,
            ArchitectureName,
            request.ClaudePath,
            ClaudeLaunchProfile.BuildArguments(
                request.Model, inputs.McpConfigurationPath, inputs.SettingsPath, inputs.SystemPromptAppendPath),
            inputs.TaskPrompt,
            prepared.SessionDirectory,
            Path.Combine(prepared.ResultsDirectory, StreamFileName),
            Path.Combine(prepared.ResultsDirectory, StandardErrorFileName),
            initialization => ClaudeLaunchProfile.FindConfigurationViolations(initialization, request.Model));

        using var limits = new RunLimitController(request.TokenBudget, request.TimeLimit, timeProvider);
        limits.Start();
        AgentProcessOutcome outcome = await AgentProcess.RunAsync(
            launch, limits, timeProvider, CancellationToken.None, string.Empty, cancellationToken);

        return new ProcessOutcome(
            outcome.Tracker,
            outcome.ConfigurationViolations,
            outcome.TerminationReason,
            limits.TimeExceeded,
            limits.TokensExceeded,
            outcome.ExitCode,
            outcome.StartedAt,
            outcome.EndedAt,
            outcome.Duration,
            outcome.RemovedEnvironmentVariables);
    }

    private static SessionRecord BuildRecord(
        SingleAgentSessionRequest request,
        PrepareRunOutput prepared,
        SessionInputs inputs,
        string cliVersion,
        ToolchainIdentity toolchain,
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
            request.ExperimentMetadata.Kind,
            request.ExperimentMetadata.Repetition,
            request.ExperimentMetadata.OrderSeed,
            request.ExperimentMetadata.OrderPosition,
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
            protectedDifferences,
            toolchain);
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
