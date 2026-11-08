using AgenticExperimentRunner.RunPreparation;
using Xunit;

public sealed class RunIntegrityTests : IDisposable
{
    private static readonly string[] InitialFiles =
    [
        "workspace/src/index.ts",
        "public-reference/tests/public/t1/find.test.ts",
        "task-specification.md",
    ];

    private readonly string agentRoot = Path.Combine(Path.GetTempPath(), $"a2-integrity-{Guid.NewGuid():N}");
    private readonly RunManifest manifest;

    public RunIntegrityTests()
    {
        foreach (string file in InitialFiles)
        {
            Write(file, $"contenuto di {file}\n");
        }

        Directory.CreateDirectory(Path.Combine(agentRoot, "agent-tests"));
        IReadOnlyList<RunManifestFile> files = RunManifest.DescribeAgentRoot(
            agentRoot,
            InitialFiles.ToDictionary(file => file, file => "origine/" + file));
        manifest = new RunManifest(
            RunManifest.CurrentSchemaVersion,
            "run-1",
            "T1",
            DateTimeOffset.UnixEpoch,
            new string('a', 40),
            "Target",
            "docs/benchmark/T1-specifica-pubblica.md",
            ["workspace", "agent-tests"],
            files.Count,
            RunManifest.ComputeAggregateSha256(files),
            files,
            []);
    }

    public void Dispose() => TestDirectories.DeleteRecursively(agentRoot);

    [Theory]
    [InlineData(IntegrityScope.AllFiles)]
    [InlineData(IntegrityScope.ProtectedOnly)]
    public void FindDifferences_UnchangedArea_ReportsNothing(IntegrityScope scope)
    {
        Assert.Empty(RunIntegrity.FindDifferences(manifest, agentRoot, scope));
    }

    [Fact]
    public void FindDifferences_ChangesInWritableDirectories_AreReportedOnlyInFullScope()
    {
        Write("workspace/src/index.ts", "soluzione dell'agente\n");
        Write("workspace/src/new.ts", "nuovo\n");
        Write("agent-tests/extra.test.ts", "test dell'agente\n");

        Assert.Equal(
            ["aggiunto: agent-tests/extra.test.ts", "aggiunto: workspace/src/new.ts", "modificato: workspace/src/index.ts"],
            RunIntegrity.FindDifferences(manifest, agentRoot, IntegrityScope.AllFiles));
        Assert.Empty(RunIntegrity.FindDifferences(manifest, agentRoot, IntegrityScope.ProtectedOnly));
    }

    [Fact]
    public void FindDifferences_ChangesToProtectedMaterial_AreReportedInBothScopes()
    {
        Write("public-reference/tests/public/t1/find.test.ts", "test indebolito\n");
        File.Delete(Path.Combine(agentRoot, "task-specification.md"));
        Write("public-reference/tests/public/t1/injected.test.ts", "aggiunto\n");
        Write("notes.md", "file in radice\n");

        string[] expected =
        [
            "aggiunto: notes.md",
            "aggiunto: public-reference/tests/public/t1/injected.test.ts",
            "mancante: task-specification.md",
            "modificato: public-reference/tests/public/t1/find.test.ts",
        ];
        Assert.Equal(expected, RunIntegrity.FindDifferences(manifest, agentRoot, IntegrityScope.AllFiles));
        Assert.Equal(expected, RunIntegrity.FindDifferences(manifest, agentRoot, IntegrityScope.ProtectedOnly));
    }

    [Fact]
    public void FindDifferences_DirectoryNamedLikeWritablePrefix_IsStillProtected()
    {
        // 'workspace-copy/' inizia come 'workspace' ma non è una directory scrivibile.
        Write("workspace-copy/file.ts", "fuori dall'area scrivibile\n");

        Assert.Equal(
            ["aggiunto: workspace-copy/file.ts"],
            RunIntegrity.FindDifferences(manifest, agentRoot, IntegrityScope.ProtectedOnly));
    }

    [Fact]
    public void FindDifferences_MissingAgentRoot_Throws()
    {
        Assert.Throws<RunPreparationException>(
            () => RunIntegrity.FindDifferences(manifest, agentRoot + "-assente", IntegrityScope.AllFiles));
    }

    [Fact]
    public void DescribeAgentRoot_FileWithoutKnownSource_Throws()
    {
        Write("workspace/untracked.ts", "non dalla baseline\n");

        Assert.Throws<RunPreparationException>(
            () => RunManifest.DescribeAgentRoot(agentRoot, InitialFiles.ToDictionary(file => file, file => file)));
    }

    private void Write(string relativePath, string content)
    {
        string path = Path.Combine(agentRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
