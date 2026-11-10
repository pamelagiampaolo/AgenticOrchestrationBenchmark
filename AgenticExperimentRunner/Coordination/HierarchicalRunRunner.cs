using AgenticExperimentRunner.RunPreparation;
using AgenticExperimentRunner.SessionExecution;

namespace AgenticExperimentRunner.Coordination;

/// <summary>
/// Parametri di una run gerarchica.
/// </summary>
/// <param name="PreparedRunPath">Percorso assoluto di <c>prepared-run.json</c>.</param>
/// <param name="ClaudePath">Eseguibile della CLI.</param>
/// <param name="McpServerPath">Eseguibile del server MCP del workspace.</param>
/// <param name="McpToolchain">Toolchain fidata per build e test.</param>
/// <param name="CoordinationServerPath">Eseguibile dell'orchestratore, avviato dalla CLI come relay.</param>
/// <param name="Model">Identificatore esatto del modello, uguale per tutti gli agenti.</param>
/// <param name="TimeLimit">Limite di <c>agentTaskDurationMs</c> della run.</param>
/// <param name="TokenBudget">Budget globale di <c>totalTokens</c>.</param>
/// <param name="MaxDelegations">Tetto tecnico delle deleghe.</param>
public sealed record HierarchicalRunRequest(
    string PreparedRunPath,
    string ClaudePath,
    string McpServerPath,
    McpDevelopmentToolchain McpToolchain,
    string CoordinationServerPath,
    string Model,
    TimeSpan TimeLimit,
    long TokenBudget,
    int MaxDelegations);

/// <summary>
/// Esegue una run della gerarchia adattiva: un coordinatore che può delegare tramite il
/// canale di coordinamento a lavoratori avviati come sessioni CLI separate, tutti sotto
/// un unico controllore di token e tempo (B4).
/// </summary>
public sealed class HierarchicalRunRunner
{
    /// <summary>Nome dell'architettura registrato nel record.</summary>
    public const string ArchitectureName = "hierarchical-adaptive";

    /// <summary>Record finale nella directory dei risultati.</summary>
    public const string RunRecordFileName = "run-record.json";

    /// <summary>Directory dei file per agente, nella directory dei risultati.</summary>
    public const string AgentsDirectoryName = "agents";

    /// <summary>Identificatore del coordinatore.</summary>
    public const string CoordinatorId = "coordinator";

    /// <summary>Ruolo del coordinatore.</summary>
    public const string CoordinatorRole = "coordinator";

    /// <summary>Ruolo dei lavoratori.</summary>
    public const string WorkerRole = "worker";

    /// <summary>Prompt ricevuto da un lavoratore, nella sua directory.</summary>
    public const string WorkerPromptFileName = "prompt.md";

    /// <summary>Istruzione del coordinatore.</summary>
    public const string CoordinatorInstructionsFileName = "coordinator.md";

    /// <summary>Istruzione dei lavoratori.</summary>
    public const string WorkerInstructionsFileName = "worker.md";

    private const string CoordinatorMcpConfigurationFileName = "coordinator-mcp-config.json";
    private const string CoordinatorSystemPromptFileName = "coordinator-system-prompt-append.md";
    private const string WorkerMcpConfigurationFileName = "worker-mcp-config.json";
    private const string WorkerSystemPromptFileName = "worker-system-prompt-append.md";

    private readonly TimeProvider timeProvider;
    private readonly string instructionsDirectory;
    private readonly bool usesStaticPlan;

    /// <summary>
    /// Crea il runner.
    /// </summary>
    /// <param name="timeProvider">Orologio per istanti, cronometro e limite di tempo.</param>
    /// <param name="instructionsDirectory">Directory con le istruzioni versionate.</param>
    public HierarchicalRunRunner(TimeProvider timeProvider, string instructionsDirectory)
        : this(timeProvider, instructionsDirectory, usesStaticPlan: false)
    {
    }

    private HierarchicalRunRunner(TimeProvider timeProvider, string instructionsDirectory, bool usesStaticPlan)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.instructionsDirectory = instructionsDirectory ?? throw new ArgumentNullException(nameof(instructionsDirectory));
        this.usesStaticPlan = usesStaticPlan;
    }

    /// <summary>Crea il runner della gerarchia statica sul runtime gerarchico condiviso.</summary>
    /// <param name="timeProvider">Orologio per metriche e limiti.</param>
    /// <param name="instructionsDirectory">Directory delle istruzioni versionate.</param>
    /// <returns>Runner configurato con i tre ruoli obbligatori.</returns>
    public static HierarchicalRunRunner CreateStatic(TimeProvider timeProvider, string instructionsDirectory) =>
        new(timeProvider, instructionsDirectory, usesStaticPlan: true);

    /// <summary>
    /// Solo per le prove dell'harness: avvolge il gestore delle deleghe (per esempio per
    /// ritardarle e osservare il comportamento della CLI su chiamate lunghe). Mai impostato
    /// dal comando, quindi assente nelle run sperimentali.
    /// </summary>
    internal Func<IDelegationHandler, IDelegationHandler>? DelegationDecorator { get; init; }

    /// <summary>
    /// Esegue la run e scrive il record.
    /// </summary>
    /// <param name="request">Parametri.</param>
    /// <param name="cancellationToken">Annullamento: termina tutti i processi e propaga l'annullamento.</param>
    /// <returns>Il record scritto e il suo percorso.</returns>
    /// <exception cref="SessionExecutionException">
    /// Area non integra, risultati già presenti, file mancanti o CLI non avviabile.
    /// </exception>
    public async Task<(HierarchicalRunRecord Record, string RecordPath)> RunAsync(
        HierarchicalRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        PrepareRunOutput prepared = RunInputs.ReadPreparedRun(request.PreparedRunPath);
        RunManifest manifest = RunInputs.ReadManifest(prepared.ManifestPath);
        EnsureResultsDirectoryUnused(prepared);
        RunInputs.EnsurePristine(prepared, manifest);

        string pipeName = CoordinationHost.CreatePipeName();
        RunConditions conditions = WriteConditions(request, prepared, pipeName);
        string cliVersion = await AgentProcess.ReadCliVersionAsync(request.ClaudePath, prepared.SessionDirectory, cancellationToken);

        using var limits = new RunLimitController(request.TokenBudget, request.TimeLimit, timeProvider);
        WorkerDelegationHandler handler;
        IDelegationHandler policy;
        StaticDelegationHandler? staticPolicy = null;
        if (usesStaticPlan)
        {
            handler = new WorkerDelegationHandler(
                (agentId, role, task) => CreateWorkerLaunch(request, prepared, conditions, agentId, role, task),
                sequence => StaticHierarchyPlan.Steps[sequence - 1].AgentId,
                sequence => StaticHierarchyPlan.Steps[sequence - 1].Role,
                limits,
                timeProvider,
                StaticHierarchyPlan.Steps.Count,
                cancellationToken);
            staticPolicy = new StaticDelegationHandler(handler);
            policy = staticPolicy;
        }
        else
        {
            handler = new WorkerDelegationHandler(
                (agentId, task) => CreateWorkerLaunch(request, prepared, conditions, agentId, WorkerRole, task),
                limits,
                timeProvider,
                request.MaxDelegations,
                cancellationToken);
            policy = handler;
        }

        CoordinationHost host = CoordinationHost.Start(pipeName, DelegationDecorator?.Invoke(policy) ?? policy);

        DateTimeOffset startedAt = limits.Start();
        AgentProcessOutcome coordinator;
        try
        {
            coordinator = await AgentProcess.RunAsync(
                CreateCoordinatorLaunch(request, prepared, conditions),
                limits,
                timeProvider,
                CancellationToken.None,
                string.Empty,
                cancellationToken);
        }
        finally
        {
            // La chiusura dell'host termina un eventuale lavoratore ancora attivo e ne attende l'esito.
            await host.DisposeAsync();
        }

        TimeSpan duration = limits.Elapsed;
        DateTimeOffset endedAt = timeProvider.GetUtcNow();
        IReadOnlyList<string> protectedDifferences =
            RunIntegrity.FindDifferences(manifest, prepared.AgentRoot, IntegrityScope.ProtectedOnly);

        HierarchicalRunRecord record = BuildRecord(
            request, prepared, conditions, cliVersion, limits, coordinator, handler,
            staticPolicy?.IsComplete ?? true,
            startedAt, endedAt, duration, protectedDifferences);
        string recordPath = Path.Combine(prepared.ResultsDirectory, RunRecordFileName);
        record.WriteTo(recordPath);
        return (record, recordPath);
    }

    /// <summary>
    /// Stato sperimentale della run, nell'ordine di priorità di A5: configurazione, limiti,
    /// misura dei token, esito del coordinatore.
    /// </summary>
    /// <param name="agents">Agenti della run, coordinatore per primo.</param>
    /// <param name="limits">Controllore della run.</param>
    /// <param name="tokensObserved">Totale contabilizzato.</param>
    /// <returns>Lo stato.</returns>
    public static SessionStatus DetermineStatus(IReadOnlyList<AgentProcessOutcome> agents, RunLimitController limits, long tokensObserved)
    {
        if (agents.Any(agent => agent.ConfigurationViolations.Count > 0))
        {
            return SessionStatus.ConfigurationRejected;
        }

        bool tokensExceeded = limits.TokensExceeded || tokensObserved > limits.TokenBudget;
        if (limits.TimeExceeded && tokensExceeded)
        {
            return SessionStatus.ResourceLimitsExceeded;
        }

        if (limits.TimeExceeded)
        {
            return SessionStatus.TimeLimitExceeded;
        }

        if (tokensExceeded)
        {
            return SessionStatus.TokenLimitExceeded;
        }

        AgentProcessOutcome coordinator = agents[0];
        if (coordinator.Tracker.Result is null)
        {
            return coordinator.ExitCode == 0 ? SessionStatus.UsageMeasurementError : SessionStatus.CliFailed;
        }

        // B4: un uso mancante non viene stimato e rende la misura della run incompleta.
        if (agents.Any(agent => agent.Tracker.Result is null && !agent.TerminatedByController))
        {
            return SessionStatus.UsageMeasurementError;
        }

        return coordinator.Succeeded ? SessionStatus.Completed : SessionStatus.CliFailed;
    }

    private static void ValidateRequest(HierarchicalRunRequest request)
    {
        foreach ((string path, string description) in new[]
        {
            (request.PreparedRunPath, "prepared-run.json"),
            (request.ClaudePath, "eseguibile della CLI"),
            (request.McpServerPath, "eseguibile del server MCP"),
            (request.McpToolchain.NodePath, "eseguibile Node.js"),
            (request.McpToolchain.VitestPath, "entry point Vitest"),
            (request.McpToolchain.TypeScriptPath, "entry point TypeScript"),
            (request.CoordinationServerPath, "eseguibile del relay di coordinamento"),
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

        if (request.TimeLimit <= TimeSpan.Zero || request.TokenBudget <= 0 || request.MaxDelegations <= 0)
        {
            throw new SessionExecutionException("Limite di tempo, budget di token e numero massimo di deleghe devono essere positivi.");
        }
    }

    /// <summary>
    /// Ogni area ospita una sola run: nella directory dei risultati devono esserci soltanto
    /// i documenti scritti da <c>prepare-run</c>.
    /// </summary>
    private static void EnsureResultsDirectoryUnused(PrepareRunOutput prepared)
    {
        string[] allowed = [Path.GetFileName(prepared.ManifestPath), PrepareRunCommand.PreparedRunFileName];
        string[] unexpected = Directory.EnumerateFileSystemEntries(prepared.ResultsDirectory)
            .Select(Path.GetFileName)
            .Where(name => !allowed.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        if (unexpected.Length > 0)
        {
            throw new SessionExecutionException(
                $"La directory dei risultati '{prepared.ResultsDirectory}' contiene già output di una sessione ({string.Join(", ", unexpected)}): ogni area ospita una sola run.");
        }
    }

    private RunConditions WriteConditions(HierarchicalRunRequest request, PrepareRunOutput prepared, string pipeName)
    {
        byte[] taskPrompt = File.ReadAllBytes(Path.Combine(prepared.AgentRoot, prepared.TaskSpecificationPath));
        string commonInstructions = RunInputs.ReadInstructions(instructionsDirectory, SingleAgentSessionRunner.CommonInstructionsFileName);
        string coordinatorInstructions = RunInputs.ReadInstructions(
            instructionsDirectory,
            usesStaticPlan ? "static-coordinator.md" : CoordinatorInstructionsFileName);
        Dictionary<string, string> roleInstructions = usesStaticPlan
            ? StaticHierarchyPlan.Steps.ToDictionary(
                step => step.Role,
                step => RunInputs.ReadInstructions(instructionsDirectory, step.InstructionFileName),
                StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkerRole] = RunInputs.ReadInstructions(instructionsDirectory, WorkerInstructionsFileName),
            };
        string workerInstructions = string.Join("\n", roleInstructions.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Value));
        string coordinatorSystemPrompt = RunInputs.CombineInstructions(commonInstructions, coordinatorInstructions);
        string workerSystemPrompt = string.Join(
            "\n",
            roleInstructions.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => RunInputs.CombineInstructions(commonInstructions, item.Value)));
        string settings = ClaudeLaunchProfile.BuildSettingsJson();
        string coordinatorMcpConfiguration = ClaudeLaunchProfile.BuildCoordinatorMcpConfigurationJson(
            request.McpServerPath, prepared.AgentRoot, prepared.WritableDirectories, request.McpToolchain,
            Path.Combine(prepared.ResultsDirectory, "mcp-worker"), Path.Combine(prepared.ResultsDirectory, "mcp-audit"),
            request.CoordinationServerPath, pipeName, request.TimeLimit, prepared.TaskId);
        string workerMcpConfiguration = ClaudeLaunchProfile.BuildMcpConfigurationJson(
            request.McpServerPath, prepared.AgentRoot, prepared.WritableDirectories, request.McpToolchain,
            Path.Combine(prepared.ResultsDirectory, "mcp-worker"), Path.Combine(prepared.ResultsDirectory, "mcp-audit"),
            prepared.TaskId);

        var conditions = new RunConditions(
            taskPrompt,
            Path.Combine(prepared.ResultsDirectory, SingleAgentSessionRunner.SettingsFileName),
            Path.Combine(prepared.ResultsDirectory, CoordinatorMcpConfigurationFileName),
            Path.Combine(prepared.ResultsDirectory, CoordinatorSystemPromptFileName),
            Path.Combine(prepared.ResultsDirectory, WorkerMcpConfigurationFileName),
            Path.Combine(prepared.ResultsDirectory, WorkerSystemPromptFileName),
            commonInstructions,
            roleInstructions,
            new HierarchyHashes(
                RunInputs.Sha256(taskPrompt),
                RunInputs.Sha256(commonInstructions),
                RunInputs.Sha256(coordinatorInstructions),
                RunInputs.Sha256(workerInstructions),
                RunInputs.Sha256(coordinatorSystemPrompt),
                RunInputs.Sha256(workerSystemPrompt),
                RunInputs.Sha256(settings),
                RunInputs.Sha256(coordinatorMcpConfiguration),
                RunInputs.Sha256(workerMcpConfiguration),
                RunInputs.Sha256(CoordinationMcpServer.DescribeTools()),
                RunInputs.Sha256(File.ReadAllBytes(request.McpServerPath)),
                RunInputs.AssemblySha256(request.McpServerPath),
                RunInputs.Sha256(File.ReadAllBytes(request.CoordinationServerPath)),
                RunInputs.AssemblySha256(request.CoordinationServerPath)));

        RunInputs.WriteNewText(conditions.SettingsPath, settings);
        RunInputs.WriteNewText(conditions.CoordinatorMcpConfigurationPath, coordinatorMcpConfiguration);
        RunInputs.WriteNewText(conditions.CoordinatorSystemPromptPath, coordinatorSystemPrompt);
        RunInputs.WriteNewText(conditions.WorkerMcpConfigurationPath, workerMcpConfiguration);
        RunInputs.WriteNewText(conditions.WorkerSystemPromptPath, workerSystemPrompt);
        Directory.CreateDirectory(Path.Combine(prepared.ResultsDirectory, AgentsDirectoryName, CoordinatorId));
        return conditions;
    }

    private static AgentLaunch CreateCoordinatorLaunch(
        HierarchicalRunRequest request,
        PrepareRunOutput prepared,
        RunConditions conditions)
    {
        string[] tools = [.. ClaudeLaunchProfile.QualifiedMcpToolNames, ClaudeLaunchProfile.QualifiedDelegateToolName];
        string[] servers = [ClaudeLaunchProfile.McpServerName, ClaudeLaunchProfile.CoordinationServerName];
        string directory = Path.Combine(prepared.ResultsDirectory, AgentsDirectoryName, CoordinatorId);
        return new AgentLaunch(
            CoordinatorId,
            CoordinatorRole,
            request.ClaudePath,
            ClaudeLaunchProfile.BuildArguments(
                request.Model, conditions.CoordinatorMcpConfigurationPath, conditions.SettingsPath, conditions.CoordinatorSystemPromptPath, tools),
            conditions.TaskPrompt,
            prepared.SessionDirectory,
            Path.Combine(directory, SingleAgentSessionRunner.StreamFileName),
            Path.Combine(directory, SingleAgentSessionRunner.StandardErrorFileName),
            initialization => ClaudeLaunchProfile.FindConfigurationViolations(initialization, request.Model, tools, servers));
    }

    private static AgentLaunch CreateWorkerLaunch(
        HierarchicalRunRequest request,
        PrepareRunOutput prepared,
        RunConditions conditions,
        string agentId,
        string role,
        string task)
    {
        string directory = Path.Combine(prepared.ResultsDirectory, AgentsDirectoryName, agentId);
        Directory.CreateDirectory(directory);
        RunInputs.WriteNewText(Path.Combine(directory, WorkerPromptFileName), task);
        string roleSystemPromptPath = Path.Combine(directory, "system-prompt-append.md");
        string roleSystemPrompt = RunInputs.CombineInstructions(
            conditions.CommonInstructions,
            conditions.RoleInstructions[role]);
        RunInputs.WriteNewText(roleSystemPromptPath, roleSystemPrompt);
        return new AgentLaunch(
            agentId,
            role,
            request.ClaudePath,
            ClaudeLaunchProfile.BuildArguments(
                request.Model, conditions.WorkerMcpConfigurationPath, conditions.SettingsPath, roleSystemPromptPath),
            RunInputs.Utf8WithoutBom.GetBytes(task),
            prepared.SessionDirectory,
            Path.Combine(directory, SingleAgentSessionRunner.StreamFileName),
            Path.Combine(directory, SingleAgentSessionRunner.StandardErrorFileName),
            initialization => ClaudeLaunchProfile.FindConfigurationViolations(initialization, request.Model));
    }

    private HierarchicalRunRecord BuildRecord(
        HierarchicalRunRequest request,
        PrepareRunOutput prepared,
        RunConditions conditions,
        string cliVersion,
        RunLimitController limits,
        AgentProcessOutcome coordinator,
        WorkerDelegationHandler handler,
        bool requiredPlanComplete,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        TimeSpan duration,
        IReadOnlyList<string> protectedDifferences)
    {
        List<AgentRecord> agents =
        [
            AgentRecord.FromOutcome(coordinator, null, Path.Combine(AgentsDirectoryName, CoordinatorId)),
            .. handler.Workers.Select(worker =>
                AgentRecord.FromOutcome(worker.Outcome, worker.RequestId, Path.Combine(AgentsDirectoryName, worker.Outcome.AgentId))),
        ];
        TokenUsage total = agents.Aggregate(TokenUsage.Zero, (sum, agent) => sum.Add(agent.CountedUsage));
        Dictionary<string, TokenUsage> usageByRole = agents
            .GroupBy(agent => agent.Role, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Aggregate(TokenUsage.Zero, (sum, agent) => sum.Add(agent.CountedUsage)), StringComparer.Ordinal);
        AgentProcessOutcome[] outcomes = [coordinator, .. handler.Workers.Select(worker => worker.Outcome)];
        SessionStatus status = DetermineStatus(outcomes, limits, total.TotalTokens);
        if (status == SessionStatus.Completed && !requiredPlanComplete)
        {
            status = SessionStatus.CliFailed;
        }
        bool overBudget = status is SessionStatus.TokenLimitExceeded or SessionStatus.ResourceLimitsExceeded;

        return new HierarchicalRunRecord(
            HierarchicalRunRecord.CurrentSchemaVersion,
            prepared.RunId,
            prepared.TaskId,
            usesStaticPlan ? StaticHierarchyPlan.ArchitectureName : ArchitectureName,
            prepared.SourceCommit,
            status,
            startedAt,
            endedAt,
            (long)duration.TotalMilliseconds,
            (long)request.TimeLimit.TotalMilliseconds,
            request.TokenBudget,
            usesStaticPlan ? StaticHierarchyPlan.Steps.Count : request.MaxDelegations,
            total.TotalTokens,
            overBudget ? total.TotalTokens - request.TokenBudget : null,
            agents.All(agent => agent.TokenMeasurementComplete),
            total,
            usageByRole,
            limits.StopReason ?? (!requiredPlanComplete ? "il coordinatore non ha completato i tre passaggi statici" : null),
            request.Model,
            cliVersion,
            agents,
            handler.Events,
            DelegationCounts.From(handler.Events.ToList()),
            MaxConcurrent(handler.Workers.Select(worker => (worker.Outcome.StartedAt, worker.Outcome.EndedAt)).ToList()),
            conditions.Hashes,
            request.ClaudePath,
            request.McpServerPath,
            request.CoordinationServerPath,
            protectedDifferences);
    }

    /// <summary>
    /// Massimo di intervalli sovrapposti: misura osservata della concorrenza dei lavoratori,
    /// non un valore assunto dalla regola "uno alla volta".
    /// </summary>
    /// <param name="intervals">Intervalli di attività dei processi.</param>
    /// <returns>Il massimo numero di processi attivi nello stesso istante.</returns>
    internal static int MaxConcurrent(IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> intervals)
    {
        // A parità di istante la fine precede l'inizio: processi consecutivi non si sovrappongono.
        var boundaries = intervals
            .SelectMany(interval => new[] { (Time: interval.Start, Change: 1), (Time: interval.End, Change: -1) })
            .OrderBy(boundary => boundary.Time)
            .ThenBy(boundary => boundary.Change);
        int active = 0;
        int maximum = 0;
        foreach ((_, int change) in boundaries)
        {
            active += change;
            maximum = Math.Max(maximum, active);
        }

        return maximum;
    }

    private sealed record RunConditions(
        byte[] TaskPrompt,
        string SettingsPath,
        string CoordinatorMcpConfigurationPath,
        string CoordinatorSystemPromptPath,
        string WorkerMcpConfigurationPath,
        string WorkerSystemPromptPath,
        string CommonInstructions,
        IReadOnlyDictionary<string, string> RoleInstructions,
        HierarchyHashes Hashes);
}
