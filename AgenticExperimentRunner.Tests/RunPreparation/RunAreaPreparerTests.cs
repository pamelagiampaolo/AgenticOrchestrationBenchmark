using System.Text;
using AgenticExperimentRunner.RunPreparation;
using Xunit;

public sealed class RunAreaPreparerTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly TemporaryGitRepository repository = new();
    private readonly string runsRoot = Path.Combine(Path.GetTempPath(), $"a1-runs-{Guid.NewGuid():N}");
    private readonly RunAreaPreparer preparer = new(new GitCommandRunner(), new FixedTimeProvider(FixedNow));

    public void Dispose()
    {
        repository.Dispose();
        TestDirectories.DeleteRecursively(runsRoot);
    }

    [Fact]
    public async Task PrepareAsync_CopiesOnlyCommittedContentOfSourceDirectory()
    {
        repository.WriteFile("Target/package.json", "{ \"name\": \"fixture\" }\n");
        repository.WriteFile("Target/src/index.ts", "export const committed = 1;\r\n");
        repository.WriteFile("Outside/secret.txt", "fuori dalla sorgente");
        string commit = repository.CommitAll("baseline");
        repository.WriteFile("Target/src/index.ts", "export const uncommitted = 2;\n");
        repository.WriteFile("Target/untracked.ts", "non versionato");

        PreparedRunArea area = await preparer.PrepareAsync(Request(commit, "run-1"), Token);

        Assert.Equal(
            ["package.json", "src/index.ts"],
            area.Manifest.Files.Select(file => file.Path));
        Assert.Equal(
            "export const committed = 1;\r\n",
            await File.ReadAllTextAsync(Path.Combine(area.WorkspacePath, "src", "index.ts"), Token));
        Assert.False(File.Exists(Path.Combine(area.WorkspacePath, "untracked.ts")));
        Assert.Equal(commit, area.Manifest.BaselineCommit);
        Assert.Equal("Target", area.Manifest.SourceDirectory);
        Assert.Equal(FixedNow, area.Manifest.PreparedAtUtc);
        Assert.Equal(2, area.Manifest.FileCount);
        Assert.Equal(
            GitTestProcess.Sha256(Encoding.UTF8.GetBytes("export const committed = 1;\r\n")),
            area.Manifest.Files.Single(file => file.Path == "src/index.ts").Sha256);
    }

    [Fact]
    public async Task PrepareAsync_WithHostAutoCrlf_KeepsExactCommittedBytes()
    {
        repository.WriteFile("Target/lf.ts", "riga1\nriga2\n");
        string commit = repository.CommitAll("baseline LF");
        // Riproduce la configurazione di sistema di Git for Windows osservata durante A1.
        repository.SetConfig("core.autocrlf", "true");

        PreparedRunArea area = await preparer.PrepareAsync(Request(commit, "run-1"), Token);

        Assert.Equal(
            "riga1\nriga2\n",
            await File.ReadAllTextAsync(Path.Combine(area.WorkspacePath, "lf.ts"), Token));
    }

    [Fact]
    public async Task PrepareAsync_WritesManifestOutsideWorkspace()
    {
        repository.WriteFile("Target/a.txt", "a");
        string commit = repository.CommitAll("baseline");

        PreparedRunArea area = await preparer.PrepareAsync(Request(commit, "run-1"), Token);

        Assert.Equal(Path.Combine(runsRoot, "run-1"), area.RunAreaPath);
        Assert.Equal(area.RunAreaPath, Path.GetDirectoryName(area.ManifestPath));
        Assert.Equal(area.RunAreaPath, Path.GetDirectoryName(area.WorkspacePath));
        Assert.Equal(area.Manifest, BaselineManifest.ReadFrom(area.ManifestPath), new ManifestComparer());
        byte[] manifestBytes = await File.ReadAllBytesAsync(area.ManifestPath, Token);
        Assert.NotEqual(0xEF, manifestBytes[0]);
    }

    [Fact]
    public async Task PrepareAsync_SameCommitTwice_HasSameAggregateHashAndDistinctRunIds()
    {
        repository.WriteFile("Target/a.txt", "a");
        repository.WriteFile("Target/dir/b.txt", "b");
        string commit = repository.CommitAll("baseline");

        PreparedRunArea first = await preparer.PrepareAsync(Request(commit, "run-1"), Token);
        PreparedRunArea second = await preparer.PrepareAsync(Request("main", "run-2"), Token);

        Assert.Equal(first.Manifest.AggregateSha256, second.Manifest.AggregateSha256);
        Assert.Equal(
            BaselineManifest.ComputeAggregateSha256(first.Manifest.Files),
            first.Manifest.AggregateSha256);
        Assert.Equal("run-1", first.Manifest.RunId);
        Assert.Equal("run-2", second.Manifest.RunId);
        Assert.NotEqual(first.WorkspacePath, second.WorkspacePath);
    }

    [Theory]
    [InlineData("Target/tests/reserved/hidden.test.ts")]
    [InlineData("Target/.npmrc")]
    [InlineData("Target/.env")]
    [InlineData("Target/node_modules/pkg/index.js")]
    public async Task PrepareAsync_CommitWithUnauthorizedMaterial_FailsAndRemovesRunArea(string forbiddenPath)
    {
        repository.WriteFile("Target/a.txt", "a");
        repository.WriteFile(forbiddenPath, "vietato");
        string commit = repository.CommitAll("baseline contaminata");

        await Assert.ThrowsAsync<RunPreparationException>(() => preparer.PrepareAsync(Request(commit, "run-1"), Token));

        Assert.False(Directory.Exists(Path.Combine(runsRoot, "run-1")));
    }

    [Fact]
    public async Task PrepareAsync_CommitWithSymbolicLink_FailsAndRemovesRunArea()
    {
        repository.WriteFile("Target/a.txt", "a");
        repository.CommitAll("baseline");
        repository.StageSymbolicLink("Target/escape", "../../outside");
        string commit = repository.CommitIndex("symlink");

        RunPreparationException exception = await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request(commit, "run-1"), Token));

        Assert.Contains("SymbolicLink", exception.Message);
        Assert.False(Directory.Exists(Path.Combine(runsRoot, "run-1")));
    }

    [Fact]
    public async Task PrepareAsync_RunsRootInsideRepository_IsRejected()
    {
        repository.WriteFile("Target/a.txt", "a");
        string commit = repository.CommitAll("baseline");
        string insideRoot = Path.Combine(repository.RootPath, "runs");

        await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request(commit, "run-1") with { RunsRootPath = insideRoot }, Token));

        Assert.False(Directory.Exists(insideRoot));
    }

    [Fact]
    public async Task PrepareAsync_ExistingRunArea_IsRejectedAndLeftUntouched()
    {
        repository.WriteFile("Target/a.txt", "a");
        string commit = repository.CommitAll("baseline");
        string existingArea = Path.Combine(runsRoot, "run-1");
        Directory.CreateDirectory(existingArea);
        string marker = Path.Combine(existingArea, "precedente.txt");
        await File.WriteAllTextAsync(marker, "dati di una run precedente", Token);

        await Assert.ThrowsAsync<RunPreparationException>(() => preparer.PrepareAsync(Request(commit, "run-1"), Token));

        Assert.True(File.Exists(marker));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("run 1")]
    [InlineData("-run")]
    [InlineData("run/1")]
    public async Task PrepareAsync_InvalidRunId_IsRejected(string runId)
    {
        repository.WriteFile("Target/a.txt", "a");
        string commit = repository.CommitAll("baseline");

        await Assert.ThrowsAsync<RunPreparationException>(() => preparer.PrepareAsync(Request(commit, runId), Token));

        Assert.False(Directory.Exists(runsRoot));
    }

    [Theory]
    [InlineData("riferimento-inesistente", "Target")]
    [InlineData("main", "Missing")]
    public async Task PrepareAsync_UnknownReferenceOrSource_FailsWithoutRunArea(string reference, string source)
    {
        repository.WriteFile("Target/a.txt", "a");
        repository.CommitAll("baseline");

        await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request(reference, "run-1") with { SourceDirectory = source }, Token));

        Assert.False(Directory.Exists(Path.Combine(runsRoot, "run-1")));
    }

    [Fact]
    public async Task PrepareAsync_ReferenceLookingLikeOption_IsNotInterpretedAsOption()
    {
        repository.WriteFile("Target/a.txt", "a");
        repository.CommitAll("baseline");

        await Assert.ThrowsAsync<RunPreparationException>(
            () => preparer.PrepareAsync(Request("--output=pwned", "run-1"), Token));

        Assert.False(File.Exists(Path.Combine(repository.RootPath, "pwned")));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private RunAreaRequest Request(string reference, string runId) =>
        new(repository.RootPath, reference, "Target", runsRoot, runId);

    /// <summary>Confronta manifest per valore, inclusi gli elenchi di file.</summary>
    private sealed class ManifestComparer : IEqualityComparer<BaselineManifest>
    {
        public bool Equals(BaselineManifest? x, BaselineManifest? y) =>
            x is not null && y is not null
            && x with { Files = Array.Empty<BaselineManifestFile>() } == y with { Files = Array.Empty<BaselineManifestFile>() }
            && x.Files.SequenceEqual(y.Files);

        public int GetHashCode(BaselineManifest obj) => obj.RunId.GetHashCode();
    }
}
