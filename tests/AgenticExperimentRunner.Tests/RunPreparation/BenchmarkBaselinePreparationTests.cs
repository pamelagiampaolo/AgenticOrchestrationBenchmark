using AgenticExperimentRunner.RunPreparation;
using Xunit;

/// <summary>
/// Prova di accettazione A2 sul repository reale: l'area preparata per T1 da HEAD
/// contiene esattamente i materiali previsti, con i byte dei blob dello stesso commit.
/// </summary>
public sealed class BenchmarkBaselinePreparationTests : IDisposable
{
    private const string SourceDirectory = "BenchmarkTarget";
    private const string PublicTestsPrefix = SourceDirectory + "/tests/public/";

    private readonly string temporaryBase = Path.Combine(Path.GetTempPath(), $"a2-benchmark-{Guid.NewGuid():N}");

    public void Dispose() => TestDirectories.DeleteRecursively(temporaryBase);

    [Fact]
    public async Task PrepareAsync_T1FromHead_MatchesCommittedMaterialsExactly()
    {
        string repositoryRoot = GitTestProcess.RunForText(AppContext.BaseDirectory, "rev-parse", "--show-toplevel");
        string commit = GitTestProcess.RunForText(repositoryRoot, "rev-parse", "HEAD");
        var preparer = new RunAreaPreparer(
            new GitCommandRunner(),
            TimeProvider.System,
            TestDirectories.TempUserProfile());

        PreparedRunArea area = await preparer.PrepareAsync(
            new RunAreaRequest(
                repositoryRoot,
                commit,
                SourceDirectory,
                "T1",
                Path.Combine(temporaryBase, "runs"),
                Path.Combine(temporaryBase, "results"),
                "acceptance-a2"),
            TestContext.Current.CancellationToken);

        string[] committedPaths = ListCommittedFiles(repositoryRoot, commit);
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in committedPaths)
        {
            string? agentPath = ExpectedAgentPath(path);
            if (agentPath is not null)
            {
                expected[agentPath] = path;
            }
        }

        expected["task-specification.md"] = "docs/benchmark/task-specifications/T1-specifica-pubblica.md";

        Assert.Equal(
            expected.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            area.Manifest.Files.Select(file => new KeyValuePair<string, string>(file.Path, file.SourcePath)));
        foreach (RunManifestFile file in area.Manifest.Files)
        {
            byte[] blob = GitTestProcess.Run(repositoryRoot, "cat-file", "blob", $"{commit}:{file.SourcePath}");
            Assert.Equal(GitTestProcess.Sha256(blob), file.Sha256);
        }

        Assert.Equal(2, area.Manifest.Files.Count(file => IsDirectlyIn(file.Path, "public-reference/tests/public/")));
        Assert.Contains(area.Manifest.Files, file => file.Path.StartsWith("public-reference/tests/public/t1/", StringComparison.Ordinal));
        Assert.Equal(
            committedPaths.Where(path => path.StartsWith(PublicTestsPrefix, StringComparison.Ordinal)
                && !path.StartsWith(PublicTestsPrefix + "t1/", StringComparison.Ordinal)
                && !IsDirectlyIn(path, PublicTestsPrefix)),
            area.Manifest.ExcludedSourcePaths);
        Assert.NotEmpty(area.Manifest.ExcludedSourcePaths);
        Assert.Equal(commit, area.Manifest.SourceCommit);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(area.AgentRootPath, "agent-tests")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(area.SessionDirectoryPath));
        Assert.Empty(RunIntegrity.FindDifferences(area.Manifest, area.AgentRootPath, IntegrityScope.AllFiles));
    }

    /// <summary>
    /// Destinazione attesa di un file del commit, ricavata in modo indipendente
    /// dall'implementazione; <see langword="null"/> se il file non va consegnato.
    /// </summary>
    private static string? ExpectedAgentPath(string repositoryPath)
    {
        if (!repositoryPath.StartsWith(SourceDirectory + "/", StringComparison.Ordinal))
        {
            return null;
        }

        string sourceRelative = repositoryPath[(SourceDirectory.Length + 1)..];
        if (!repositoryPath.StartsWith(PublicTestsPrefix, StringComparison.Ordinal))
        {
            return "workspace/" + sourceRelative;
        }

        bool isCommonSuite = IsDirectlyIn(repositoryPath, PublicTestsPrefix);
        bool isT1Suite = repositoryPath.StartsWith(PublicTestsPrefix + "t1/", StringComparison.Ordinal);
        return isCommonSuite || isT1Suite ? "public-reference/" + sourceRelative : null;
    }

    private static bool IsDirectlyIn(string path, string directoryPrefix) =>
        path.StartsWith(directoryPrefix, StringComparison.Ordinal)
        && !path[directoryPrefix.Length..].Contains('/');

    private static string[] ListCommittedFiles(string repositoryRoot, string commit) =>
        GitTestProcess.RunForText(repositoryRoot, "ls-tree", "-r", "-z", "--name-only", commit)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
