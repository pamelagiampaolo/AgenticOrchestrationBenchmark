using AgentWorkspaceMcpServer;
using Xunit;

namespace AgenticExperimentRunner.Tests;

public sealed class EphemeralBuildRunnerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"build-runner-{Guid.NewGuid():N}");

    [Fact]
    public async Task RunAsync_BuildsRealBaselineWithoutWritingWorkspace()
    {
        string repositoryRoot = FindRepositoryRoot();
        string benchmark = Path.Combine(repositoryRoot, "benchmark", "BenchmarkTarget");
        string agentRoot = Path.Combine(root, "agent-root");
        string workspace = Path.Combine(agentRoot, "workspace");
        Directory.CreateDirectory(workspace);
        CopyDirectory(Path.Combine(benchmark, "src"), Path.Combine(workspace, "src"));
        foreach (string fileName in new[] { "tsconfig.json", "package.json" })
        {
            File.Copy(Path.Combine(benchmark, fileName), Path.Combine(workspace, fileName));
        }
        string workerRoot = Path.Combine(root, "worker");
        string auditPath = Path.Combine(root, "results", "audit.jsonl");
        var runner = new EphemeralBuildRunner(
            agentRoot,
            workerRoot,
            FindNode(),
            Path.Combine(benchmark, "node_modules", "typescript", "lib", "tsc.js"),
            auditPath,
            TimeSpan.FromSeconds(30));

        ControlledCommandResult result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal("completed", result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(workspace, "dist")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(workerRoot));
        string audit = Assert.Single(await File.ReadAllLinesAsync(auditPath, TestContext.Current.CancellationToken));
        Assert.Contains("\"operation\":\"build\"", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("--noEmit", audit, StringComparison.Ordinal);
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
