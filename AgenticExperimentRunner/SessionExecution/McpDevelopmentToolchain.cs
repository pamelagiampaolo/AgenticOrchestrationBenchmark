namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Percorsi assoluti della toolchain fidata usata dai comandi di sviluppo MCP.
/// </summary>
/// <param name="NodePath">Eseguibile Node.js fissato per la run.</param>
/// <param name="VitestPath">Entry point Vitest fissato per la run.</param>
/// <param name="TypeScriptPath">Entry point <c>tsc.js</c> fissato per la run.</param>
public sealed record McpDevelopmentToolchain(
    string NodePath,
    string VitestPath,
    string TypeScriptPath);
