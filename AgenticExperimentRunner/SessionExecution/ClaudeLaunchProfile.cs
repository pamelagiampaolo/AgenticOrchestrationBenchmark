using System.Text.Json;

namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Profilo di avvio isolato della CLI, ripreso dalle opzioni verificate in P1, P1.1
/// e P7. Produce argomenti e file di configurazione; non avvia processi.
/// </summary>
public static class ClaudeLaunchProfile
{
    /// <summary>Nome con cui il server MCP è registrato nella sessione.</summary>
    public const string McpServerName = "benchmark_workspace";

    /// <summary>Nome con cui il server di coordinamento è registrato nella sessione del coordinatore.</summary>
    public const string CoordinationServerName = "benchmark_coordination";

    /// <summary>Nome completo dello strumento di delega come lo dichiara la CLI.</summary>
    public const string QualifiedDelegateToolName = "mcp__" + CoordinationServerName + "__delegate_task";

    /// <summary>Strumenti del server MCP esposti al modello.</summary>
    public static readonly IReadOnlyList<string> McpToolNames =
        ["list_files", "read_file", "write_file", "delete_file", "move_file"];

    /// <summary>Strumenti built-in esplicitamente negati, come nel profilo verificato.</summary>
    public static readonly IReadOnlyList<string> DisallowedBuiltInTools =
        ["Bash", "PowerShell", "Read", "Write", "Edit", "Glob", "Grep", "WebFetch", "WebSearch", "Task", "Agent", "Skill"];

    // Fine riga fisso: l'hash della configurazione non deve dipendere dal sistema operativo.
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, NewLine = "\n" };

    /// <summary>Nomi completi degli strumenti MCP come li dichiara la CLI.</summary>
    public static IReadOnlyList<string> QualifiedMcpToolNames =>
        McpToolNames.Select(name => $"mcp__{McpServerName}__{name}").ToList();

    /// <summary>
    /// Argomenti della CLI. Il prompt non compare: viene inviato su stdin.
    /// </summary>
    /// <param name="model">Identificatore esatto del modello.</param>
    /// <param name="mcpConfigurationPath">File di configurazione MCP.</param>
    /// <param name="settingsPath">File delle impostazioni fidate.</param>
    /// <param name="systemPromptAppendPath">File con il testo aggiunto al system prompt.</param>
    /// <returns>Gli argomenti, uno per elemento.</returns>
    public static IReadOnlyList<string> BuildArguments(
        string model,
        string mcpConfigurationPath,
        string settingsPath,
        string systemPromptAppendPath) =>
        BuildArguments(model, mcpConfigurationPath, settingsPath, systemPromptAppendPath, QualifiedMcpToolNames);

    /// <summary>
    /// Argomenti della CLI con un insieme esplicito di strumenti consentiti
    /// (per esempio quelli del coordinatore). Il prompt non compare: viene inviato su stdin.
    /// </summary>
    /// <param name="model">Identificatore esatto del modello.</param>
    /// <param name="mcpConfigurationPath">File di configurazione MCP.</param>
    /// <param name="settingsPath">File delle impostazioni fidate.</param>
    /// <param name="systemPromptAppendPath">File con il testo aggiunto al system prompt.</param>
    /// <param name="allowedTools">Nomi completi degli strumenti MCP consentiti.</param>
    /// <returns>Gli argomenti, uno per elemento.</returns>
    public static IReadOnlyList<string> BuildArguments(
        string model,
        string mcpConfigurationPath,
        string settingsPath,
        string systemPromptAppendPath,
        IReadOnlyList<string> allowedTools) =>
    [
        "-p",
        "--model", model,
        "--output-format", "stream-json",
        "--verbose",
        "--include-partial-messages",
        "--include-hook-events",
        "--restricted",
        "--strict-mcp-config",
        "--mcp-config", mcpConfigurationPath,
        "--settings", settingsPath,
        "--setting-sources", "",
        "--tools", "",
        "--allowedTools", string.Join(',', allowedTools),
        "--disallowedTools", string.Join(',', DisallowedBuiltInTools),
        "--disable-slash-commands",
        "--no-session-persistence",
        "--no-chrome",
        "--permission-mode", "dontAsk",
        "--permission-prompts", "none",
        "--append-system-prompt-file", systemPromptAppendPath,
    ];

    /// <summary>
    /// Impostazioni fidate passate con <c>--settings</c>.
    /// </summary>
    /// <returns>JSON indentato.</returns>
    public static string BuildSettingsJson() => JsonSerializer.Serialize(
        new Dictionary<string, object>
        {
            ["disableAllHooks"] = true,
            ["env"] = new Dictionary<string, string>
            {
                ["CLAUDE_AGENT_SDK_DISABLE_BUILTIN_AGENTS"] = "1",
                ["ENABLE_CLAUDEAI_MCP_SERVERS"] = "false",

                // Una delega resta in attesa per tutta la sessione del lavoratore: senza questa
                // impostazione la CLI la sposterebbe in background dopo 120 s (documentazione
                // delle variabili d'ambiente, consultata l'8 novembre 2026). Uguale per tutte le
                // architetture, perché il single-agent non ha chiamate MCP di quella durata.
                ["CLAUDE_CODE_MCP_AUTO_BACKGROUND_MS"] = "0",
            },
        },
        JsonOptions);

    /// <summary>
    /// Configurazione MCP con il solo server dell'area agente.
    /// </summary>
    /// <param name="mcpServerPath">Eseguibile del server MCP.</param>
    /// <param name="agentRoot">Root assoluta assegnata al server.</param>
    /// <param name="writableDirectories">Directory scrivibili relative ad <paramref name="agentRoot"/>.</param>
    /// <returns>JSON indentato.</returns>
    public static string BuildMcpConfigurationJson(
        string mcpServerPath,
        string agentRoot,
        IReadOnlyList<string> writableDirectories) => JsonSerializer.Serialize(
        new Dictionary<string, object>
        {
            ["mcpServers"] = new Dictionary<string, object>
            {
                [McpServerName] = WorkspaceServer(mcpServerPath, agentRoot, writableDirectories),
            },
        },
        JsonOptions);

    /// <summary>
    /// Configurazione MCP del coordinatore: server dell'area agente e relay di coordinamento.
    /// </summary>
    /// <param name="mcpServerPath">Eseguibile del server MCP dell'area agente.</param>
    /// <param name="agentRoot">Root assoluta assegnata al server.</param>
    /// <param name="writableDirectories">Directory scrivibili relative ad <paramref name="agentRoot"/>.</param>
    /// <param name="coordinationServerPath">Eseguibile dell'orchestratore, avviato come relay.</param>
    /// <param name="pipeName">Pipe creata dall'host di coordinamento.</param>
    /// <param name="toolTimeout">
    /// Timeout per server: alza la finestra d'inattività delle chiamate di delega
    /// (30 minuti predefiniti per stdio) almeno a questo valore.
    /// </param>
    /// <returns>JSON indentato.</returns>
    public static string BuildCoordinatorMcpConfigurationJson(
        string mcpServerPath,
        string agentRoot,
        IReadOnlyList<string> writableDirectories,
        string coordinationServerPath,
        string pipeName,
        TimeSpan toolTimeout) => JsonSerializer.Serialize(
        new Dictionary<string, object>
        {
            ["mcpServers"] = new Dictionary<string, object>
            {
                [McpServerName] = WorkspaceServer(mcpServerPath, agentRoot, writableDirectories),
                [CoordinationServerName] = new Dictionary<string, object>
                {
                    ["command"] = coordinationServerPath,
                    ["args"] = new[] { "coordination-server", "--pipe", pipeName },
                    ["timeout"] = (long)Math.Ceiling(toolTimeout.TotalMilliseconds),
                },
            },
        },
        JsonOptions);

    /// <summary>
    /// Confronta la configurazione dichiarata dalla CLI con quella attesa.
    /// </summary>
    /// <param name="initialization">Evento <c>system/init</c>.</param>
    /// <param name="requestedModel">Modello richiesto.</param>
    /// <returns>Differenze riscontrate; vuoto se la configurazione è quella attesa.</returns>
    public static IReadOnlyList<string> FindConfigurationViolations(SessionInitialization initialization, string requestedModel) =>
        FindConfigurationViolations(initialization, requestedModel, QualifiedMcpToolNames, [McpServerName]);

    /// <summary>
    /// Confronta la configurazione dichiarata dalla CLI con strumenti e server attesi espliciti.
    /// </summary>
    /// <param name="initialization">Evento <c>system/init</c>.</param>
    /// <param name="requestedModel">Modello richiesto.</param>
    /// <param name="expectedTools">Nomi completi degli strumenti attesi, esattamente.</param>
    /// <param name="expectedServers">Server MCP attesi, tutti nello stato <c>connected</c>.</param>
    /// <returns>Differenze riscontrate; vuoto se la configurazione è quella attesa.</returns>
    public static IReadOnlyList<string> FindConfigurationViolations(
        SessionInitialization initialization,
        string requestedModel,
        IReadOnlyList<string> expectedTools,
        IReadOnlyList<string> expectedServers)
    {
        var violations = new List<string>();
        if (initialization.Model != requestedModel)
        {
            violations.Add($"modello '{initialization.Model}' invece di '{requestedModel}'");
        }

        string[] sortedExpectedTools = expectedTools.Order(StringComparer.Ordinal).ToArray();
        string[] actualTools = initialization.Tools.Order(StringComparer.Ordinal).ToArray();
        if (!sortedExpectedTools.SequenceEqual(actualTools))
        {
            violations.Add($"strumenti [{string.Join(", ", actualTools)}] invece di [{string.Join(", ", sortedExpectedTools)}]");
        }

        string[] expectedServerStates = expectedServers.Select(name => $"{name}:connected").Order(StringComparer.Ordinal).ToArray();
        string[] actualServerStates = initialization.McpServers
            .Select(server => $"{server.Name}:{server.Status}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!expectedServerStates.SequenceEqual(actualServerStates))
        {
            violations.Add(
                $"server MCP [{string.Join(", ", actualServerStates)}] invece di [{string.Join(", ", expectedServerStates)}]");
        }

        AddIfNotEmpty(violations, "agenti", initialization.Agents);
        AddIfNotEmpty(violations, "skill", initialization.Skills);
        AddIfNotEmpty(violations, "comandi slash", initialization.SlashCommands);
        return violations;
    }

    private static Dictionary<string, object> WorkspaceServer(
        string mcpServerPath,
        string agentRoot,
        IReadOnlyList<string> writableDirectories) => new()
    {
        ["command"] = mcpServerPath,
        ["args"] = new[] { agentRoot },
        ["env"] = new Dictionary<string, string>
        {
            ["ENABLE_CLAUDEAI_MCP_SERVERS"] = "false",
            ["MCP_WRITABLE_DIRECTORIES"] = string.Join(Path.PathSeparator, writableDirectories),
        },
    };

    private static void AddIfNotEmpty(List<string> violations, string description, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
        {
            violations.Add($"{description} presenti: {string.Join(", ", values)}");
        }
    }
}

/// <summary>
/// Ambiente del processo della CLI.
/// </summary>
public static class ChildProcessEnvironment
{
    private static readonly string[] RemovedPrefixes = ["CLAUDE", "ANTHROPIC"];

    /// <summary>
    /// Rimuove le variabili ereditate che possono cambiare il comportamento della CLI
    /// (per esempio quelle impostate da una sessione Claude Code che avvia l'orchestratore).
    /// </summary>
    /// <param name="environment">Ambiente da modificare sul posto.</param>
    /// <returns>Nomi rimossi, ordinati; i valori non vengono mai restituiti.</returns>
    public static IReadOnlyList<string> RemoveInheritedAgentVariables(IDictionary<string, string?> environment)
    {
        List<string> removed = environment.Keys
            .Where(name => RemovedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal)
            .ToList();
        foreach (string name in removed)
        {
            environment.Remove(name);
        }

        return removed;
    }
}
