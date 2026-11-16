using AgentWorkspaceMcpServer;
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
            "import fs from 'node:fs'; if (!fs.existsSync('src/value.ts') || !fs.existsSync('tests/public/value.test.ts')) process.exit(9); fs.writeFileSync('../output/vitest-results.json', JSON.stringify({numTotalTests:1,numPassedTests:1,numFailedTests:0,numPendingTests:0,numTotalTestSuites:1,numPassedTestSuites:1,numFailedTestSuites:0,numPendingTestSuites:0}));",
            TestContext.Current.CancellationToken);
        string workerRoot = Path.Combine(root, "worker");
        string auditPath = Path.Combine(root, "results", "audit.jsonl");
        var runner = new EphemeralPublicTestRunner(
            agentRoot,
            workerRoot,
            FindNode(),
            script,
            auditPath,
            TimeSpan.FromSeconds(10),
            "T1");

        ControlledCommandResult result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal("completed", result.Status);
        Assert.Equal(1, result.Tests?.Passed);
        Assert.True(File.Exists(publicSource));
        Assert.Empty(Directory.EnumerateFileSystemEntries(workerRoot));
        Assert.Single(await File.ReadAllLinesAsync(auditPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_ExecutesRealPublicVitestSuite()
    {
        string repositoryRoot = FindRepositoryRoot();
        string benchmark = Path.Combine(repositoryRoot, "benchmark", "BenchmarkTarget");
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
            TimeSpan.FromSeconds(30),
            "T1");

        ControlledCommandResult result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal("completed", result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(27, result.Tests?.Total);
        Assert.Equal(27, result.Tests?.Passed);
        Assert.DoesNotContain(Path.Combine(root, "real-worker"), result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "real-worker")));
    }

    [Fact]
    public async Task RunAsync_ClassifiesMissingReportAsInfrastructureError()
    {
        string agentRoot = CreateAgentRoot();
        string script = Path.Combine(root, "no-report.mjs");
        await File.WriteAllTextAsync(script, "console.log(process.cwd());", TestContext.Current.CancellationToken);
        string workerRoot = Path.Combine(root, "missing-report-worker");
        var runner = new EphemeralPublicTestRunner(
            agentRoot,
            workerRoot,
            FindNode(),
            script,
            Path.Combine(root, "results", "missing-report-audit.jsonl"),
            TimeSpan.FromSeconds(10),
            "T1");

        ControlledCommandResult result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal("infrastructure-error", result.Status);
        Assert.Null(result.Tests);
        Assert.DoesNotContain(workerRoot, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<worker>", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_T6_SelectsOnlyApplicablePublicTests()
    {
        string benchmark = Path.Combine(FindRepositoryRoot(), "benchmark", "BenchmarkTarget");
        string agentRoot = CreateAgentRoot();
        CopyDirectory(Path.Combine(benchmark, "src"), Path.Combine(agentRoot, "workspace", "src"));
        File.Copy(Path.Combine(benchmark, "tsconfig.json"), Path.Combine(agentRoot, "workspace", "tsconfig.json"));
        string publicSource = Path.Combine(benchmark, "tests", "public");
        string publicDestination = Path.Combine(agentRoot, "public-reference", "tests", "public");
        Directory.CreateDirectory(publicDestination);
        foreach (string testName in new[] { "inventory-management.test.ts", "order-management.test.ts" })
        {
            File.Copy(Path.Combine(publicSource, testName), Path.Combine(publicDestination, testName));
        }
        CopyDirectory(Path.Combine(publicSource, "t6"), Path.Combine(publicDestination, "t6"));
        string workerRoot = Path.Combine(root, "t6-worker");
        var runner = new EphemeralPublicTestRunner(
            agentRoot,
            workerRoot,
            FindNode(),
            Path.Combine(benchmark, "node_modules", "vitest", "vitest.mjs"),
            Path.Combine(root, "results", "t6-audit.jsonl"),
            TimeSpan.FromSeconds(30),
            "T6");

        ControlledCommandResult result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(32, result.Tests?.Total);
        Assert.Equal(21, result.Tests?.Passed);
        Assert.Equal(5, result.Tests?.Failed);
        Assert.Equal(6, result.Tests?.Pending);
        Assert.Empty(Directory.EnumerateFileSystemEntries(workerRoot));
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
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "benchmark", "BenchmarkTarget")))
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
