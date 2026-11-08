namespace McpReadOnlyPrototype;

/// <summary>
/// Esegue la build TypeScript nella copia effimera senza modificare la workspace dell'agente.
/// </summary>
public sealed class EphemeralBuildRunner
{
    private readonly EphemeralTypecheckRunner runner;

    /// <summary>
    /// Configura workspace, toolchain, audit e timeout del comando di build fisso.
    /// </summary>
    public EphemeralBuildRunner(
        string agentRoot,
        string workerRoot,
        string nodePath,
        string typeScriptEntryPoint,
        string auditPath,
        TimeSpan timeout)
    {
        runner = new EphemeralTypecheckRunner(
            agentRoot,
            workerRoot,
            nodePath,
            typeScriptEntryPoint,
            auditPath,
            timeout,
            "build",
            noEmit: false);
    }

    /// <summary>
    /// Esegue <c>tsc --project tsconfig.json</c> e restituisce il risultato controllato.
    /// </summary>
    public Task<ControlledCommandResult> RunAsync(CancellationToken cancellationToken = default) =>
        runner.RunAsync(cancellationToken);
}
