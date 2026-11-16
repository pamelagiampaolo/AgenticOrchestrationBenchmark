using System.Text.Json;
using AgentWorkspaceMcpServer;
using Xunit;

namespace AgenticExperimentRunner.Tests;

public sealed class ControlledPublicTestRunnerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"controlled-tests-{Guid.NewGuid():N}");

    public ControlledPublicTestRunnerTests()
    {
        Directory.CreateDirectory(root);
    }

    [Fact]
    public async Task RunAsync_UsesFixedArgumentsAndWritesAuditRecord()
    {
        string workingDirectory = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
        string scriptPath = Path.Combine(root, "fake-vitest.mjs");
        string configPath = Path.Combine(root, "trusted.config.ts");
        string auditPath = Path.Combine(root, "results", "commands.jsonl");
        await File.WriteAllTextAsync(
            scriptPath,
            "console.log(JSON.stringify(process.argv.slice(2)));\n",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            configPath,
            "export default {};\n",
            TestContext.Current.CancellationToken);

        var runner = new ControlledPublicTestRunner(
            GetNodePath(),
            scriptPath,
            configPath,
            workingDirectory,
            auditPath,
            TimeSpan.FromSeconds(10));

        ControlledCommandResult result = await runner.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal("completed", result.Status);
        Assert.Contains("--reporter=json", result.StandardOutput, StringComparison.Ordinal);
        string auditLine = Assert.Single(await File.ReadAllLinesAsync(
            auditPath,
            TestContext.Current.CancellationToken));
        using JsonDocument audit = JsonDocument.Parse(auditLine);
        Assert.Equal("public-tests", audit.RootElement.GetProperty("operation").GetString());
        Assert.Equal(workingDirectory, audit.RootElement.GetProperty("workingDirectory").GetString());
    }

    [Fact]
    public void Constructor_RejectsAuditInsideExecutedProject()
    {
        string workingDirectory = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
        string scriptPath = Path.Combine(root, "fake-vitest.mjs");
        string configPath = Path.Combine(root, "trusted.config.ts");
        File.WriteAllText(scriptPath, string.Empty);
        File.WriteAllText(configPath, string.Empty);

        Assert.Throws<ArgumentException>(() => new ControlledPublicTestRunner(
            GetNodePath(),
            scriptPath,
            configPath,
            workingDirectory,
            Path.Combine(workingDirectory, "audit.jsonl"),
            TimeSpan.FromSeconds(10)));
    }

    private static string GetNodePath()
    {
        string executableName = OperatingSystem.IsWindows() ? "node.exe" : "node";
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Node.js non disponibile nel PATH della prova.");
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
