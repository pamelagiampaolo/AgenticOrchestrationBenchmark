using AgenticExperimentRunner.RunPreparation;
using Xunit;

/// <summary>
/// I test considerano soltanto i conflitti sotto la cartella temporanea: le directory
/// superiori reali (per esempio il profilo dell'utente che contiene %TEMP%) non
/// dipendono dal test.
/// </summary>
public sealed class SessionAncestorPolicyTests : IDisposable
{
    private readonly string temporaryBase = Path.Combine(Path.GetTempPath(), $"a2-ancestors-{Guid.NewGuid():N}");

    private string Start => Path.Combine(temporaryBase, "outer", "runs");

    public void Dispose() => TestDirectories.DeleteRecursively(temporaryBase);

    [Fact]
    public void FindConflicts_CleanAncestors_ReportsNothingUnderTemporaryBase()
    {
        Directory.CreateDirectory(Start);

        Assert.Empty(ConflictsUnderBase(SessionAncestorPolicy.FindConflicts(Start, userProfileDirectory: null)));
    }

    [Fact]
    public void FindConflicts_ReportsEntriesInStartAndAllAncestors_NearestFirst()
    {
        CreateFile(Path.Combine(Start, ".mcp.json"));
        CreateFile(Path.Combine(temporaryBase, "outer", "CLAUDE.local.md"));
        Directory.CreateDirectory(Path.Combine(temporaryBase, ".git"));

        Assert.Equal(
            [
                Path.Combine(Start, ".mcp.json"),
                Path.Combine(temporaryBase, "outer", "CLAUDE.local.md"),
                Path.Combine(temporaryBase, ".git"),
            ],
            ConflictsUnderBase(SessionAncestorPolicy.FindConflicts(Start, userProfileDirectory: null)));
    }

    [Fact]
    public void FindConflicts_StartDirectoryMayNotExistYet()
    {
        CreateFile(Path.Combine(temporaryBase, "CLAUDE.md"));

        Assert.Equal(
            [Path.Combine(temporaryBase, "CLAUDE.md")],
            ConflictsUnderBase(SessionAncestorPolicy.FindConflicts(Start, userProfileDirectory: null)));
    }

    [Fact]
    public void FindConflicts_ClaudeDirectoryInUserProfile_IsTolerated()
    {
        string profile = Path.Combine(temporaryBase, "outer");
        Directory.CreateDirectory(Path.Combine(profile, ".claude"));
        Directory.CreateDirectory(Path.Combine(temporaryBase, ".claude"));

        Assert.Equal(
            [Path.Combine(temporaryBase, ".claude")],
            ConflictsUnderBase(SessionAncestorPolicy.FindConflicts(Start, profile + Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void FindConflicts_InstructionsInUserProfile_AreReported()
    {
        string profile = Path.Combine(temporaryBase, "outer");
        CreateFile(Path.Combine(profile, "CLAUDE.md"));

        Assert.Equal(
            [Path.Combine(profile, "CLAUDE.md")],
            ConflictsUnderBase(SessionAncestorPolicy.FindConflicts(Start, profile)));
    }

    private IEnumerable<string> ConflictsUnderBase(IReadOnlyList<string> conflicts) =>
        conflicts.Where(path => path.StartsWith(temporaryBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    private static void CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "conflitto");
    }
}
