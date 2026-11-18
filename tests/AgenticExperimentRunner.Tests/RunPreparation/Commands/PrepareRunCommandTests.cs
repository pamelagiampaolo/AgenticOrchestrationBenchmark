using System.Text.Json;
using System.Text.RegularExpressions;
using AgenticExperimentRunner.RunPreparation;
using Xunit;

public sealed class PrepareRunCommandTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 8, 12, 34, 56, TimeSpan.Zero);

    private readonly TemporaryGitRepository repository = new();
    private readonly string temporaryBase = Path.Combine(Path.GetTempPath(), $"a2-cli-{Guid.NewGuid():N}");
    private readonly PrepareRunCommand command;
    private readonly StringWriter standardOutput = new();
    private readonly StringWriter standardError = new();

    public PrepareRunCommandTests()
    {
        command = new PrepareRunCommand(
            new RunAreaPreparer(
                new GitCommandRunner(),
                new FixedTimeProvider(FixedNow),
                TestDirectories.TempUserProfile()),
            new FixedTimeProvider(FixedNow));
    }

    private string RunsRoot => Path.Combine(temporaryBase, "runs");

    private string ResultsRoot => Path.Combine(temporaryBase, "results");

    public void Dispose()
    {
        repository.Dispose();
        TestDirectories.DeleteRecursively(temporaryBase);
    }

    [Fact]
    public async Task ExecuteAsync_ValidArguments_WritesSingleJsonDocumentConsistentWithDisk()
    {
        string commit = CommitBenchmarkLikeBaseline();

        int exitCode = await command.ExecuteAsync(Arguments("--run-id", "cli-run"), standardOutput, standardError, Token);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, standardError.ToString());
        using JsonDocument document = JsonDocument.Parse(standardOutput.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(
            [
                "schemaVersion", "runId", "taskId", "sourceCommit", "runAreaPath", "agentRoot",
                "sessionDirectory", "resultsDirectory", "manifestPath", "workspaceDirectory",
                "agentTestsDirectory", "publicReferenceDirectory", "taskSpecificationPath", "writableDirectories",
            ],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("cli-run", root.GetProperty("runId").GetString());
        Assert.Equal("T1", root.GetProperty("taskId").GetString());
        Assert.Equal(commit, root.GetProperty("sourceCommit").GetString());

        string agentRoot = root.GetProperty("agentRoot").GetString()!;
        Assert.Equal(Path.Combine(RunsRoot, "cli-run", "agent-root"), agentRoot);
        Assert.Equal(Path.Combine(RunsRoot, "cli-run", "session"), root.GetProperty("sessionDirectory").GetString());
        Assert.Equal(Path.Combine(ResultsRoot, "cli-run"), root.GetProperty("resultsDirectory").GetString());
        Assert.True(File.Exists(root.GetProperty("manifestPath").GetString()));
        Assert.Equal(
            standardOutput.ToString().ReplaceLineEndings("\n"),
            await File.ReadAllTextAsync(Path.Combine(ResultsRoot, "cli-run", "prepared-run.json"), Token));
        Assert.True(Directory.Exists(Path.Combine(agentRoot, root.GetProperty("workspaceDirectory").GetString()!)));
        Assert.True(Directory.Exists(Path.Combine(agentRoot, root.GetProperty("agentTestsDirectory").GetString()!)));
        Assert.True(Directory.Exists(Path.Combine(agentRoot, root.GetProperty("publicReferenceDirectory").GetString()!)));
        Assert.True(File.Exists(Path.Combine(agentRoot, root.GetProperty("taskSpecificationPath").GetString()!)));
        Assert.Equal(
            ["workspace", "agent-tests"],
            root.GetProperty("writableDirectories").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutRunId_GeneratesSortableRunId()
    {
        CommitBenchmarkLikeBaseline();

        int exitCode = await command.ExecuteAsync(Arguments(), standardOutput, standardError, Token);

        Assert.Equal(0, exitCode);
        using JsonDocument document = JsonDocument.Parse(standardOutput.ToString());
        string runId = document.RootElement.GetProperty("runId").GetString()!;
        Assert.Matches(new Regex("^20261108T123456Z-[0-9a-f]{8}$"), runId);
        Assert.True(Directory.Exists(Path.Combine(RunsRoot, runId)));
    }

    [Fact]
    public void CreateDefaultRunId_ProducesDistinctValuesAtSameInstant()
    {
        Assert.NotEqual(command.CreateDefaultRunId(), command.CreateDefaultRunId());
    }

    [Theory]
    [InlineData("--unknown", "x")]
    [InlineData("--task")]
    [InlineData("--task", "T1", "--task", "T2")]
    [InlineData("--run-id", "")]
    public async Task ExecuteAsync_InvalidArguments_ReturnsUsageErrorWithEmptyOutput(params string[] extra)
    {
        int exitCode = await command.ExecuteAsync(Arguments(extra), standardOutput, standardError, Token);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.StartsWith("Errore negli argomenti:", standardError.ToString());
    }

    [Theory]
    [InlineData("--repository")]
    [InlineData("--commit")]
    [InlineData("--task")]
    [InlineData("--runs-root")]
    [InlineData("--results-root")]
    public async Task ExecuteAsync_MissingRequiredOption_ReturnsUsageError(string option)
    {
        List<string> arguments = Arguments().ToList();
        int index = arguments.IndexOf(option);
        arguments.RemoveRange(index, 2);

        int exitCode = await command.ExecuteAsync(arguments, standardOutput, standardError, Token);

        Assert.Equal(2, exitCode);
        Assert.Contains(option, standardError.ToString());
        Assert.Equal(string.Empty, standardOutput.ToString());
    }

    [Theory]
    [InlineData("--repository")]
    [InlineData("--runs-root")]
    [InlineData("--results-root")]
    public async Task ExecuteAsync_RelativePath_ReturnsUsageError(string option)
    {
        List<string> arguments = Arguments().ToList();
        arguments[arguments.IndexOf(option) + 1] = "relativo";

        int exitCode = await command.ExecuteAsync(arguments, standardOutput, standardError, Token);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.False(Directory.Exists(temporaryBase));
    }

    [Fact]
    public async Task ExecuteAsync_PreparationFailure_ReturnsOneWithEmptyOutput()
    {
        CommitBenchmarkLikeBaseline();
        List<string> arguments = Arguments().ToList();
        arguments[arguments.IndexOf("--task") + 1] = "T2";

        int exitCode = await command.ExecuteAsync(arguments, standardOutput, standardError, Token);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, standardOutput.ToString());
        Assert.StartsWith("Preparazione della run non riuscita:", standardError.ToString());
        Assert.False(Directory.Exists(temporaryBase));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string[] Arguments(params string[] extra) =>
    [
        "--repository", repository.RootPath,
        "--commit", "main",
        "--task", "T1",
        "--runs-root", RunsRoot,
        "--results-root", ResultsRoot,
        .. extra,
    ];

    /// <summary>Baseline con la sorgente fissa del comando, <c>benchmark/BenchmarkTarget</c>.</summary>
    private string CommitBenchmarkLikeBaseline()
    {
        repository.WriteFile("benchmark/BenchmarkTarget/src/index.ts", "export {};\n");
        repository.WriteFile("benchmark/BenchmarkTarget/tests/public/common.test.ts", "// comune\n");
        repository.WriteFile("benchmark/BenchmarkTarget/tests/public/t1/find.test.ts", "// t1\n");
        repository.WriteFile("docs/benchmark/task-specifications/T1-specifica-pubblica.md", "# T1\n");
        return repository.CommitAll("baseline");
    }
}
