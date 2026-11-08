using McpReadOnlyPrototype;
using Xunit;

namespace AgenticExperimentRunner.Tests;

public sealed class EphemeralPublicTestRunnerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"ephemeral-public-tests-{Guid.NewGuid():N}");

    public EphemeralPublicTestRunnerTests()
    {
        Directory.CreateDirectory(root);
    }

    [Fact]
    public async Task RunAsync_ComposesAndRemovesWorkerWhilePreservingPublicTests()
    {
        string agentRoot = CreateAgentRoot();
        string workspaceSource = Path.Combine(agentRoot, "workspace", "src", "value.ts");
        string publicSource = Path.Combine(agentRoot, "public-reference", "tests", "public", "value.test.ts");
        Directory.CreateDirectory(Path.GetDirectoryName(workspaceSource)!);
        Directory.CreateDirectory(Path.GetDirectoryName(publicSource)!);
        await File.WriteAllTextAsync(workspaceSource, "export const value = 1;", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(publicSource, "test", TestContext.Current.CancellationToken);
        string script = Path.Combine(root, "trusted-vitest.mjs");
        await File.WriteAllTextAsync(
            script,
            "import fs from 'node:fs'; if (!fs.existsSync('src/value.ts') || !fs.existsSync('tests/public/value.test.ts')) process.exit(9);",
            TestContext.Current.CancellationToken);
        string workerRoot = Path.Combine(root, "worker");
        string auditPath = Path.Combine(root, "results", "audit.jsonl");
        var runner = new EphemeralPublicTestRunner(
            agentRoot,
            workerRoot,
            FindNode(),
            script,
            auditPath,
            TimeSpan.FromSeconds(10));

        ControlledCommandResult result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal("completed", result.Status);
        Assert.True(File.Exists(publicSource));
        Assert.Empty(Directory.EnumerateFileSystemEntries(workerRoot));
        Assert.Single(await File.ReadAllLinesAsync(auditPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_ExecutesRealPublicVitestSuite()
    {
        string repositoryRoot = FindRepositoryRoot();
        string benchmark = Path.Combine(repositoryRoot, "BenchmarkTarget");
        string agentRoot = CreateAgentRoot();
        CopyDirectory(Path.Combine(benchmark, "src"), Path.Combine(agentRoot, "workspace", "src"));
        File.Copy(
            Path.Combine(benchmark, "tsconfig.json"),
            Path.Combine(agentRoot, "workspace", "tsconfig.json"));
        string publicDestination = Path.Combine(agentRoot, "public-reference", "tests", "public");
        Directory.CreateDirectory(publicDestination);
        foreach (string testName in new[] { "inventory-management.test.ts", "order-management.test.ts" })
        {
            File.Copy(Path.Combine(benchmark, "tests", "public", testName), Path.Combine(publicDestination, testName));
        }

        var runner = new EphemeralPublicTestRunner(
            agentRoot,
            Path.Combine(root, "real-worker"),
            FindNode(),
            Path.Combine(benchmark, "node_modules", "vitest", "vitest.mjs"),
            Path.Combine(root, "results", "real-audit.jsonl"),
            TimeSpan.FromSeconds(30));

        ControlledCommandResult result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal("completed", result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("JSON report written", result.StandardOutput, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "real-worker")));
    }

    private string CreateAgentRoot()
    {
        string agentRoot = Path.Combine(root, $"agent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(agentRoot, "workspace"));
        Directory.CreateDirectory(Path.Combine(agentRoot, "public-reference"));
        Directory.CreateDirectory(Path.Combine(agentRoot, "agent-tests"));
        return agentRoot;
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
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "BenchmarkTarget")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Root del repository non trovata dalla directory dei test.");
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
