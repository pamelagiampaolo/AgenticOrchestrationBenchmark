namespace AgentWorkspaceMcpServer;

/// <summary>
/// Compone una copia effimera del progetto ed esegue esclusivamente i test creati dall'agente.
/// Il risultato fornisce feedback di sviluppo e non contribuisce alla valutazione ufficiale.
/// </summary>
public sealed class EphemeralAgentTestRunner
{
    private readonly EphemeralPublicTestRunner runner;
    private readonly string agentTestsRoot;

    /// <summary>
    /// Configura l'area dei test dell'agente e la toolchain Vitest fidata.
    /// </summary>
    public EphemeralAgentTestRunner(
        string agentRoot,
        string workerRoot,
        string nodePath,
        string vitestEntryPoint,
        string auditPath,
        TimeSpan timeout)
    {
        agentTestsRoot = Path.Combine(Path.GetFullPath(agentRoot), "agent-tests");
        runner = new EphemeralPublicTestRunner(
            agentRoot,
            workerRoot,
            nodePath,
            vitestEntryPoint,
            auditPath,
            timeout,
            "agent-tests",
            "tests/agent/**/*.test.ts",
            "agent-tests",
            "dell'agente",
            ["tests/reserved/**", "tests/public/**"]);
    }

    /// <summary>
    /// Esegue i soli file sotto <c>agent-tests/tests/agent</c> in una composizione nuova.
    /// </summary>
    /// <param name="cancellationToken">Segnale di annullamento dell'invocazione.</param>
    /// <returns>Esito strutturato e conteggi Vitest separati dalla valutazione.</returns>
    public Task<ControlledCommandResult> RunAsync(CancellationToken cancellationToken = default)
    {
        EnsureOnlyAgentTestPaths();
        return runner.RunAsync(cancellationToken);
    }

    private void EnsureOnlyAgentTestPaths()
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(
            agentTestsRoot,
            "*",
            SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(agentTestsRoot, entry)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            bool isAllowed = relativePath.Equals("tests", StringComparison.OrdinalIgnoreCase)
                || relativePath.Equals(Path.Combine("tests", "agent"), StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith(
                    Path.Combine("tests", "agent") + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);
            if (!isAllowed)
            {
                throw new IOException($"Percorso non ammesso nell'area dei test dell'agente: '{relativePath}'.");
            }
        }
    }
}
