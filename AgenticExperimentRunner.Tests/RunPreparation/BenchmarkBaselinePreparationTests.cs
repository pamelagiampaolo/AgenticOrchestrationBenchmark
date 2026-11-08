using AgenticExperimentRunner.RunPreparation;
using Xunit;

/// <summary>
/// Prova di accettazione A1 sul repository reale: la workspace preparata da HEAD
/// coincide con i file versionati di BenchmarkTarget nello stesso commit.
/// </summary>
public sealed class BenchmarkBaselinePreparationTests : IDisposable
{
    private const string SourceDirectory = "BenchmarkTarget";

    private readonly string runsRoot = Path.Combine(Path.GetTempPath(), $"a1-benchmark-runs-{Guid.NewGuid():N}");

    public void Dispose() => TestDirectories.DeleteRecursively(runsRoot);

    [Fact]
    public async Task PrepareAsync_FromHead_MatchesCommittedBenchmarkTargetExactly()
    {
        string repositoryRoot = GitTestProcess.RunForText(AppContext.BaseDirectory, "rev-parse", "--show-toplevel");
        string commit = GitTestProcess.RunForText(repositoryRoot, "rev-parse", "HEAD");
        var preparer = new RunAreaPreparer(new GitCommandRunner(), TimeProvider.System);

        PreparedRunArea area = await preparer.PrepareAsync(
            new RunAreaRequest(repositoryRoot, commit, SourceDirectory, runsRoot, "acceptance-a1"),
            TestContext.Current.CancellationToken);

        Dictionary<string, string> expected = ReadCommittedFileHashes(repositoryRoot, commit);
        Assert.NotEmpty(expected);
        Assert.Equal(
            expected.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            area.Manifest.Files.Select(file => new KeyValuePair<string, string>(file.Path, file.Sha256)));
        Assert.Equal(commit, area.Manifest.BaselineCommit);
        Assert.DoesNotContain(area.Manifest.Files, file => file.Path.StartsWith("node_modules/", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(area.WorkspacePath, ".git")));
        Assert.False(File.Exists(Path.Combine(area.WorkspacePath, ".git")));
    }

    private static Dictionary<string, string> ReadCommittedFileHashes(string repositoryRoot, string commit)
    {
        string prefix = SourceDirectory + "/";
        string listing = GitTestProcess.RunForText(
            repositoryRoot, "ls-tree", "-r", "-z", "--name-only", commit, "--", SourceDirectory);

        return listing
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .ToDictionary(
                path => path[prefix.Length..],
                path => GitTestProcess.Sha256(GitTestProcess.Run(repositoryRoot, "cat-file", "blob", $"{commit}:{path}")));
    }
}
