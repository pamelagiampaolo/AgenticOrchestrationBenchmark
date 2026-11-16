using System.Diagnostics;
using System.Text.Json;

namespace AgenticExperimentRunner.SessionExecution;

/// <summary>
/// Identità della toolchain usata dai comandi MCP di sviluppo: i percorsi da soli non
/// bastano, perché lo stesso percorso (per esempio di un Node gestito da nvm) può
/// corrispondere a versioni diverse in run diverse (B4: stesso ambiente).
/// </summary>
/// <param name="NodePath">Eseguibile Node.js.</param>
/// <param name="NodeVersion">Output di <c>node --version</c>.</param>
/// <param name="NodeExecutableSha256">Hash dell'eseguibile Node.js.</param>
/// <param name="VitestPath">Entry point Vitest.</param>
/// <param name="VitestVersion">Versione dal <c>package.json</c> del pacchetto <c>vitest</c>, se trovato.</param>
/// <param name="VitestEntrySha256">Hash dell'entry point Vitest.</param>
/// <param name="TypeScriptPath">Entry point TypeScript.</param>
/// <param name="TypeScriptVersion">Versione dal <c>package.json</c> del pacchetto <c>typescript</c>, se trovato.</param>
/// <param name="TypeScriptEntrySha256">Hash dell'entry point TypeScript.</param>
public sealed record ToolchainIdentity(
    string NodePath,
    string NodeVersion,
    string NodeExecutableSha256,
    string VitestPath,
    string? VitestVersion,
    string VitestEntrySha256,
    string TypeScriptPath,
    string? TypeScriptVersion,
    string TypeScriptEntrySha256)
{
    private static readonly TimeSpan VersionProbeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Rileva l'identità della toolchain.
    /// </summary>
    /// <param name="toolchain">Percorsi passati al server MCP.</param>
    /// <param name="workingDirectory">Directory di lavoro di <c>node --version</c>.</param>
    /// <param name="cancellationToken">Annullamento.</param>
    /// <returns>L'identità rilevata.</returns>
    /// <exception cref="SessionExecutionException"><c>node --version</c> non risponde o fallisce.</exception>
    public static async Task<ToolchainIdentity> InspectAsync(
        McpDevelopmentToolchain toolchain,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolchain);
        return new ToolchainIdentity(
            toolchain.NodePath,
            await ReadNodeVersionAsync(toolchain.NodePath, workingDirectory, cancellationToken),
            RunInputs.Sha256(File.ReadAllBytes(toolchain.NodePath)),
            toolchain.VitestPath,
            FindPackageVersion(toolchain.VitestPath, "vitest"),
            RunInputs.Sha256(File.ReadAllBytes(toolchain.VitestPath)),
            toolchain.TypeScriptPath,
            FindPackageVersion(toolchain.TypeScriptPath, "typescript"),
            RunInputs.Sha256(File.ReadAllBytes(toolchain.TypeScriptPath)));
    }

    /// <summary>
    /// Versione del pacchetto npm che contiene un file: il primo <c>package.json</c>
    /// risalendo le directory il cui <c>name</c> è quello atteso.
    /// </summary>
    /// <param name="entryPointPath">File del pacchetto.</param>
    /// <param name="packageName">Nome del pacchetto npm.</param>
    /// <returns>La versione, oppure <see langword="null"/> se il pacchetto non viene trovato.</returns>
    public static string? FindPackageVersion(string entryPointPath, string packageName)
    {
        for (DirectoryInfo? directory = new FileInfo(entryPointPath).Directory; directory is not null; directory = directory.Parent)
        {
            string manifestPath = Path.Combine(directory.FullName, "package.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                JsonElement root = manifest.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("name", out JsonElement name)
                    && name.ValueKind == JsonValueKind.String
                    && name.GetString() == packageName)
                {
                    return root.TryGetProperty("version", out JsonElement version) && version.ValueKind == JsonValueKind.String
                        ? version.GetString()
                        : null;
                }
            }
            catch (JsonException)
            {
                // Un package.json illeggibile non identifica il pacchetto: si continua a risalire.
            }
        }

        return null;
    }

    private static async Task<string> ReadNodeVersionAsync(string nodePath, string workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(nodePath)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--version");
        ChildProcessEnvironment.RemoveInheritedAgentVariables(startInfo.Environment);

        using Process process = AgentProcess.StartProcess(startInfo);
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
            AgentProcess.Terminate(process);
            throw new SessionExecutionException($"'{nodePath} --version' non ha risposto entro {VersionProbeTimeout.TotalSeconds} secondi.");
        }

        string version = (await output).Trim();
        if (process.ExitCode != 0 || version.Length == 0)
        {
            throw new SessionExecutionException(
                $"'{nodePath} --version' non ha restituito una versione (codice {process.ExitCode}): {await error}");
        }

        return version;
    }
}
