using AgentWorkspaceMcpServer;
using Xunit;

namespace AgenticExperimentRunner.Tests;

public sealed class EphemeralTypecheckRunnerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"typecheck-runner-{Guid.NewGuid():N}");

    [Fact]
    public async Task RunAsync_TypechecksRealBaselineAndRemovesWorker()
    {
        string repositoryRoot = FindRepositoryRoot();
        string benchmark = Path.Combine(repositoryRoot, "benchmark", "BenchmarkTarget");
        string agentRoot = Path.Combine(root, "agent-root");
        string workspace = Path.Combine(agentRoot, "workspace");
        Directory.CreateDirectory(workspace);
        CopyDirectory(Path.Combine(benchmark, "src"), Path.Combine(workspace, "src"));
        File.Copy(Path.Combine(benchmark, "tsconfig.json"), Path.Combine(workspace, "tsconfig.json"));
        File.Copy(Path.Combine(benchmark, "package.json"), Path.Combine(workspace, "package.json"));
        string workerRoot = Path.Combine(root, "worker");
        string auditPath = Path.Combine(root, "results", "audit.jsonl");
        var runner = new EphemeralTypecheckRunner(
            agentRoot,
            workerRoot,
            FindNode(),
            Path.Combine(benchmark, "node_modules", "typescript", "lib", "tsc.js"),
            auditPath,
            TimeSpan.FromSeconds(30));

        ControlledCommandResult result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.True(
            result.Status == "completed",
            $"Stato: {result.Status}; stdout: {result.StandardOutput}; stderr: {result.StandardError}");
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workerRoot));
        Assert.Single(await File.ReadAllLinesAsync(auditPath, TestContext.Current.CancellationToken));
    }

    private static string FindNode()
    {
        string executable = OperatingSystem.IsWindows() ? "node.exe" : "node";
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, executable))
            .First(File.Exists);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "benchmark", "BenchmarkTarget")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Root repository non trovata.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
