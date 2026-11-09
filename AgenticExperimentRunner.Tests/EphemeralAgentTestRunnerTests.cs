using System.Text.Json;
using McpReadOnlyPrototype;
using Xunit;

namespace AgenticExperimentRunner.Tests;

public sealed class EphemeralAgentTestRunnerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"ephemeral-agent-tests-{Guid.NewGuid():N}");

    public EphemeralAgentTestRunnerTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task RunAsync_ComposesOnlyAgentTestsAndRecordsSeparateOperation()
    {
        string agentRoot = CreateAgentRoot();
        string source = Path.Combine(agentRoot, "workspace", "src", "value.ts");
        string agentTest = Path.Combine(agentRoot, "agent-tests", "tests", "agent", "value.test.ts");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        Directory.CreateDirectory(Path.GetDirectoryName(agentTest)!);
        await File.WriteAllTextAsync(source, "export const value = 1;", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(agentTest, "test", TestContext.Current.CancellationToken);
        string script = Path.Combine(root, "trusted-vitest.mjs");
        await File.WriteAllTextAsync(
            script,
            "import fs from 'node:fs'; if (!fs.existsSync('src/value.ts') || !fs.existsSync('tests/agent/value.test.ts') || fs.existsSync('tests/public')) process.exit(9); fs.writeFileSync('../output/vitest-results.json', JSON.stringify({numTotalTests:1,numPassedTests:1,numFailedTests:0,numPendingTests:0,numTotalTestSuites:1,numPassedTestSuites:1,numFailedTestSuites:0,numPendingTestSuites:0}));",
            TestContext.Current.CancellationToken);
        string auditPath = Path.Combine(root, "results", "audit.jsonl");
        var runner = CreateRunner(agentRoot, script, auditPath);

        ControlledCommandResult result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal("agent-tests", result.Operation);
        Assert.Equal("completed", result.Status);
        Assert.Equal(1, result.Tests?.Passed);
        using JsonDocument audit = JsonDocument.Parse(Assert.Single(await File.ReadAllLinesAsync(
            auditPath,
            TestContext.Current.CancellationToken)));
        Assert.Equal("agent-tests", audit.RootElement.GetProperty("operation").GetString());
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "worker")));
    }

    [Fact]
    public async Task RunAsync_RejectsContentOutsideTestsAgent()
    {
        string agentRoot = CreateAgentRoot();
        string forbidden = Path.Combine(agentRoot, "agent-tests", "tests", "public", "leak.test.ts");
        Directory.CreateDirectory(Path.GetDirectoryName(forbidden)!);
        await File.WriteAllTextAsync(forbidden, "test", TestContext.Current.CancellationToken);
        string script = Path.Combine(root, "trusted-vitest.mjs");
        await File.WriteAllTextAsync(script, string.Empty, TestContext.Current.CancellationToken);
        var runner = CreateRunner(agentRoot, script, Path.Combine(root, "results", "audit.jsonl"));

        IOException exception = await Assert.ThrowsAsync<IOException>(() =>
            runner.RunAsync(TestContext.Current.CancellationToken));

        Assert.Contains(Path.Combine("tests", "public"), exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private EphemeralAgentTestRunner CreateRunner(string agentRoot, string script, string auditPath) => new(
        agentRoot,
        Path.Combine(root, "worker"),
        FindNode(),
        script,
        auditPath,
        TimeSpan.FromSeconds(10));

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

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
