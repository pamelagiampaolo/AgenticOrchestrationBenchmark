using System.Text;
using AgenticExperimentRunner.RunPreparation;
using Xunit;

public sealed class RunAreaPreparerTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly string? UserProfile = TestDirectories.TempUserProfile();

    private const string SpecificationContent = "# T1\n\nSpecifica pubblica di prova.\n";

    private readonly TemporaryGitRepository repository = new();
    private readonly string temporaryBase = Path.Combine(Path.GetTempPath(), $"a2-roots-{Guid.NewGuid():N}");
    private readonly RunAreaPreparer preparer = new(new GitCommandRunner(), new FixedTimeProvider(FixedNow), UserProfile);

    private string RunsRoot => Path.Combine(temporaryBase, "runs");

    private string ResultsRoot => Path.Combine(temporaryBase, "results");

    public void Dispose()
    {
        repository.Dispose();
        TestDirectories.DeleteRecursively(temporaryBase);
    }

    [Fact]
    public async Task PrepareAsync_RoutesCommittedMaterialsIntoAgentLayout()
    {
        string commit = CommitStandardBaseline();
        repository.WriteFile("Target/src/index.ts", "export const uncommitted = 2;\n");
        repository.WriteFile("Target/untracked.ts", "non versionato");

        PreparedRunArea area = await preparer.PrepareAsync(Request(commit, "run-1"), Token);

        Assert.Equal(
            [
                "public-reference/tests/public/common.test.ts",
                "public-reference/tests/public/t1/find.test.ts",
                "task-specification.md",
                "workspace/package.json",
                "workspace/src/index.ts",
            ],
            area.Manifest.Files.Select(file => file.Path));
        Assert.Equal(
            ["Target/tests/public/t2/receive.test.ts"],
            area.Manifest.ExcludedSourcePaths);
        Assert.Equal(
            "docs/benchmark/task-specifications/T1-specifica-pubblica.md",
            area.Manifest.Files.Single(file => file.Path == "task-specification.md").SourcePath);
        Assert.Equal(
            "Target/tests/public/t1/find.test.ts",
            area.Manifest.Files.Single(file => file.Path == "public-reference/tests/public/t1/find.test.ts").SourcePath);
        Assert.Equal(
            "export const committed = 1;\r\n",
            await File.ReadAllTextAsync(Path.Combine(area.AgentRootPath, "workspace", "src", "index.ts"), Token));
        Assert.Equal(
            SpecificationContent,
            await File.ReadAllTextAsync(Path.Combine(area.AgentRootPath, "task-specification.md"), Token));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(area.AgentRootPath, "agent-tests")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(area.SessionDirectoryPath));
        Assert.Equal(commit, area.Manifest.SourceCommit);
        Assert.Equal("T1", area.Manifest.TaskId);
        Assert.Equal("Target", area.Manifest.SourceDirectory);
        Assert.Equal(FixedNow, area.Manifest.PreparedAtUtc);
        Assert.Equal(["workspace", "agent-tests"], area.Manifest.WritableDirectories);
        Assert.Equal(5, area.Manifest.FileCount);
        Assert.Equal(
            GitTestProcess.Sha256(Encoding.UTF8.GetBytes("export const committed = 1;\r\n")),
            area.Manifest.Files.Single(file => file.Path == "workspace/src/index.ts").Sha256);
    }

    [Fact]
    public async Task PrepareAsync_CreatesOnlyAgentRootAndSessionInRunArea_AndManifestInResults()
    {
        string commit = CommitStandardBaseline();

        PreparedRunArea area = await preparer.PrepareAsync(Request(commit, "run-1"), Token);

        Assert.Equal(Path.Combine(RunsRoot, "run-1"), area.RunAreaPath);
        Assert.Equal(Path.Combine(area.RunAreaPath, "agent-root"), area.AgentRootPath);
        Assert.Equal(Path.Combine(area.RunAreaPath, "session"), area.SessionDirectoryPath);
        Assert.Equal(
            ["agent-root", "session"],
            Directory.EnumerateFileSystemEntries(area.RunAreaPath).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["agent-tests", "public-reference", "task-specification.md", "workspace"],
            Directory.EnumerateFileSystemEntries(area.AgentRootPath).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(Path.Combine(ResultsRoot, "run-1", "manifest.json"), area.ManifestPath);
        Assert.Equal(area.Manifest, RunManifest.ReadFrom(area.ManifestPath), new ManifestComparer());
        byte[] manifestBytes = await File.ReadAllBytesAsync(area.ManifestPath, Token);
        Assert.NotEqual(0xEF, manifestBytes[0]);
    }

    [Fact]
    public async Task PrepareAsync_LowercaseTaskId_IsNormalized()
    {
        string commit = CommitStandardBaseline();

        PreparedRunArea area = await preparer.PrepareAsync(Request(commit, "run-1") with { TaskId = "t1" }, Token);

        Assert.Equal("T1", area.Manifest.TaskId);
    }

    [Fact]
    public async Task PrepareAsync_WithHostAutoCrlf_KeepsExactCommittedBytes()
    {
        CommitStandardBaseline();
        repository.WriteFile("Target/lf.ts", "riga1\nriga2\n");
        string commit = repository.CommitAll("baseline LF");
        // Riproduce la configurazione di sistema di Git for Windows osservata durante A1.
        repository.SetConfig("core.autocrlf", "true");

        PreparedRunArea area = await preparer.PrepareAsync(Request(commit, "run-1"), Token);

        Assert.Equal(
            "riga1\nriga2\n",
            await File.ReadAllTextAsync(Path.Combine(area.AgentRootPath, "workspace", "lf.ts"), Token));
        Assert.Equal(
            SpecificationContent,
            await File.ReadAllTextAsync(Path.Combine(area.AgentRootPath, "task-specification.md"), Token));
    }

    [Fact]
    public async Task PrepareAsync_SameCommitTwice_HasSameAggregateHashAndDistinctRunIds()
    {
        string commit = CommitStandardBaseline();

        PreparedRunArea first = await preparer.PrepareAsync(Request(commit, "run-1"), Token);
        PreparedRunArea second = await preparer.PrepareAsync(Request("main", "run-2"), Token);

        Assert.Equal(first.Manifest.AggregateSha256, second.Manifest.AggregateSha256);
        Assert.Equal(RunManifest.ComputeAggregateSha256(first.Manifest.Files), first.Manifest.AggregateSha256);
        Assert.Equal("run-1", first.Manifest.RunId);
        Assert.Equal("run-2", second.Manifest.RunId);
        Assert.NotEqual(first.AgentRootPath, second.AgentRootPath);
    }

    [Theory]
    [InlineData("Target/tests/reserved/hidden.test.ts")]
    [InlineData("Target/.npmrc")]
    [InlineData("Target/.env")]
    [InlineData("Target/node_modules/pkg/index.js")]
    [InlineData("Target/.claude/settings.json")]
    [InlineData("Target/CLAUDE.md")]
    [InlineData("Target/src/CLAUDE.local.md")]
    [InlineData("Target/AGENTS.md")]
    [InlineData("Target/.mcp.json")]
    [InlineData("Target/tests/public/helpers/setup.ts")]
    public async Task PrepareAsync_CommitWithUnauthorizedMaterial_FailsAndRemovesEverything(string forbiddenPath)
    {
        CommitStandardBaseline();
        repository.WriteFile(forbiddenPath, "vietato");
        string commit = repository.CommitAll("baseline contaminata");

        await Assert.ThrowsAsync<RunPreparationException>(() => preparer.PrepareAsync(Request(commit, "run-1"), Token));

        AssertNothingCreated("run-1");
    }

    [Fact]
    public async Task PrepareAsync_CommitWithSymbolicLink_FailsAndRemovesEverything()
    {
        CommitStandardBaseline();
        repository.StageSymbolicLink("Target/escape", "../../outside");
        string commit = repository.CommitIndex("symlink");

        RunPreparationException exception = await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request(commit, "run-1"), Token));

        Assert.Contains("SymbolicLink", exception.Message);
        AssertNothingCreated("run-1");
    }

    [Theory]
    [InlineData("T3")]
    [InlineData("T4")]
    public async Task PrepareAsync_MissingTaskMaterials_FailsWithoutCreatingDirectories(string taskId)
    {
        CommitStandardBaseline();
        // T3 non ha la specifica; T4 ha la specifica ma non la suite pubblica.
        repository.WriteFile("docs/benchmark/task-specifications/T4-specifica-pubblica.md", "# T4\n");
        string commit = repository.CommitAll("materiali incompleti");

        RunPreparationException exception = await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request(commit, "run-1") with { TaskId = taskId }, Token));

        Assert.Contains("Manca", exception.Message);
        Assert.False(Directory.Exists(temporaryBase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("T0")]
    [InlineData("X1")]
    [InlineData("T1x")]
    [InlineData("../T1")]
    public async Task PrepareAsync_InvalidTaskId_IsRejected(string taskId)
    {
        string commit = CommitStandardBaseline();

        await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request(commit, "run-1") with { TaskId = taskId }, Token));

        Assert.False(Directory.Exists(temporaryBase));
    }

    [Fact]
    public async Task PrepareAsync_RunsRootInsideRepository_IsRejected()
    {
        string commit = CommitStandardBaseline();
        string insideRoot = Path.Combine(repository.RootPath, "runs");

        await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request(commit, "run-1") with { RunsRootPath = insideRoot }, Token));

        Assert.False(Directory.Exists(insideRoot));
    }

    [Fact]
    public async Task PrepareAsync_ResultsRootInsideRepository_IsRejected()
    {
        string commit = CommitStandardBaseline();
        string insideRoot = Path.Combine(repository.RootPath, "results");

        await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request(commit, "run-1") with { ResultsRootPath = insideRoot }, Token));

        Assert.False(Directory.Exists(insideRoot));
        Assert.False(Directory.Exists(temporaryBase));
    }

    [Theory]
    [InlineData("runs", "runs")]
    [InlineData("runs", "runs/results")]
    [InlineData("results/runs", "results")]
    public async Task PrepareAsync_OverlappingRoots_AreRejected(string runsRelative, string resultsRelative)
    {
        string commit = CommitStandardBaseline();
        RunAreaRequest request = Request(commit, "run-1") with
        {
            RunsRootPath = Path.Combine(temporaryBase, runsRelative),
            ResultsRootPath = Path.Combine(temporaryBase, resultsRelative),
        };

        await Assert.ThrowsAsync<RunPreparationException>(() => preparer.PrepareAsync(request, Token));

        Assert.False(Directory.Exists(temporaryBase));
    }

    [Fact]
    public async Task PrepareAsync_ExistingRunArea_IsRejectedAndLeftUntouched()
    {
        string commit = CommitStandardBaseline();
        string marker = await CreateMarkerAsync(Path.Combine(RunsRoot, "run-1"));

        await Assert.ThrowsAsync<RunPreparationException>(() => preparer.PrepareAsync(Request(commit, "run-1"), Token));

        Assert.True(File.Exists(marker));
        Assert.False(Directory.Exists(Path.Combine(ResultsRoot, "run-1")));
    }

    [Fact]
    public async Task PrepareAsync_ExistingResultsDirectory_IsRejectedAndLeftUntouched()
    {
        string commit = CommitStandardBaseline();
        string marker = await CreateMarkerAsync(Path.Combine(ResultsRoot, "run-1"));

        await Assert.ThrowsAsync<RunPreparationException>(() => preparer.PrepareAsync(Request(commit, "run-1"), Token));

        Assert.True(File.Exists(marker));
        Assert.False(Directory.Exists(Path.Combine(RunsRoot, "run-1")));
    }

    [Theory]
    [InlineData("CLAUDE.md", false)]
    [InlineData(".mcp.json", false)]
    [InlineData(".git", true)]
    [InlineData(".claude", true)]
    public async Task PrepareAsync_ConflictingAncestorEntry_FailsBeforeCreatingDirectories(string name, bool isDirectory)
    {
        string commit = CommitStandardBaseline();
        CreateEntry(Path.Combine(temporaryBase, name), isDirectory);

        RunPreparationException exception = await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request(commit, "run-1"), Token));

        Assert.Contains(name, exception.Message);
        Assert.False(Directory.Exists(RunsRoot));
        Assert.False(Directory.Exists(ResultsRoot));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("run 1")]
    [InlineData("-run")]
    [InlineData("run/1")]
    [InlineData("run-1\n")]
    public async Task PrepareAsync_InvalidRunId_IsRejected(string runId)
    {
        string commit = CommitStandardBaseline();

        await Assert.ThrowsAsync<RunPreparationException>(() => preparer.PrepareAsync(Request(commit, runId), Token));

        Assert.False(Directory.Exists(temporaryBase));
    }

    [Theory]
    [InlineData("riferimento-inesistente", "Target")]
    [InlineData("main", "Missing")]
    public async Task PrepareAsync_UnknownReferenceOrSource_FailsWithoutCreatingDirectories(string reference, string source)
    {
        CommitStandardBaseline();

        await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request(reference, "run-1") with { SourceDirectory = source }, Token));

        Assert.False(Directory.Exists(temporaryBase));
    }

    [Fact]
    public async Task PrepareAsync_ReferenceLookingLikeOption_IsNotInterpretedAsOption()
    {
        CommitStandardBaseline();

        await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request("--output=pwned", "run-1"), Token));

        Assert.False(File.Exists(Path.Combine(repository.RootPath, "pwned")));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private RunAreaRequest Request(string reference, string runId) =>
        new(repository.RootPath, reference, "Target", "T1", RunsRoot, ResultsRoot, runId);

    /// <summary>
    /// Baseline minima con la stessa struttura di BenchmarkTarget: sorgenti, suite
    /// comune, suite di due task e specifica di T1.
    /// </summary>
    private string CommitStandardBaseline()
    {
        repository.WriteFile("Target/package.json", "{ \"name\": \"fixture\" }\n");
        repository.WriteFile("Target/src/index.ts", "export const committed = 1;\r\n");
        repository.WriteFile("Target/tests/public/common.test.ts", "// comune\n");
        repository.WriteFile("Target/tests/public/t1/find.test.ts", "// t1\n");
        repository.WriteFile("Target/tests/public/t2/receive.test.ts", "// t2\n");
        repository.WriteFile("docs/benchmark/task-specifications/T1-specifica-pubblica.md", SpecificationContent);
        repository.WriteFile("Outside/secret.txt", "fuori dalla sorgente");
        return repository.CommitAll("baseline");
    }

    private void AssertNothingCreated(string runId)
    {
        Assert.False(Directory.Exists(Path.Combine(RunsRoot, runId)));
        Assert.False(Directory.Exists(Path.Combine(ResultsRoot, runId)));
    }

    private static async Task<string> CreateMarkerAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        string marker = Path.Combine(directory, "precedente.txt");
        await File.WriteAllTextAsync(marker, "dati di una run precedente", Token);
        return marker;
    }

    private static void CreateEntry(string path, bool isDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (isDirectory)
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            File.WriteAllText(path, "conflitto");
        }
    }

    /// <summary>Confronta manifest per valore, inclusi gli elenchi.</summary>
    private sealed class ManifestComparer : IEqualityComparer<RunManifest>
    {
        public bool Equals(RunManifest? x, RunManifest? y) =>
            x is not null && y is not null
            && WithoutLists(x) == WithoutLists(y)
            && x.Files.SequenceEqual(y.Files)
            && x.WritableDirectories.SequenceEqual(y.WritableDirectories)
            && x.ExcludedSourcePaths.SequenceEqual(y.ExcludedSourcePaths);

        public int GetHashCode(RunManifest obj) => obj.RunId.GetHashCode();

        private static RunManifest WithoutLists(RunManifest manifest) =>
            manifest with
            {
                Files = Array.Empty<RunManifestFile>(),
                WritableDirectories = Array.Empty<string>(),
                ExcludedSourcePaths = Array.Empty<string>(),
            };
    }
}
