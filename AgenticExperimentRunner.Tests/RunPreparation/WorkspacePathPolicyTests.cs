using AgenticExperimentRunner.RunPreparation;
using Xunit;

public sealed class WorkspacePathPolicyTests
{
    [Theory]
    [InlineData("package.json", "package.json")]
    [InlineData("src/index.ts", "src/index.ts")]
    [InlineData("tests/public/t1/find.test.ts", "tests/public/t1/find.test.ts")]
    [InlineData("src/", "src")]
    [InlineData(".gitignore", ".gitignore")]
    [InlineData("tests/reserved-notes.md", "tests/reserved-notes.md")]
    [InlineData("BenchmarkTarget", "BenchmarkTarget")]
    public void NormalizeAuthorizedPath_AcceptsAuthorizedPaths(string archivePath, string expected)
    {
        Assert.Equal(expected, WorkspacePathPolicy.NormalizeAuthorizedPath(archivePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/etc/passwd")]
    [InlineData("../outside.txt")]
    [InlineData("src/../../outside.txt")]
    [InlineData("src/./index.ts")]
    [InlineData("src//index.ts")]
    [InlineData("src\\index.ts")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("file.txt:stream")]
    public void NormalizeAuthorizedPath_RejectsMalformedPaths(string archivePath)
    {
        Assert.Throws<RunPreparationException>(() => WorkspacePathPolicy.NormalizeAuthorizedPath(archivePath));
    }

    [Theory]
    [InlineData(".git/config")]
    [InlineData("sub/.git")]
    [InlineData(".GIT/HEAD")]
    [InlineData("node_modules/vitest/package.json")]
    [InlineData(".gitconfig")]
    [InlineData(".npmrc")]
    [InlineData("config/.npmrc")]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData("tests/reserved")]
    [InlineData("tests/reserved/t1.test.ts")]
    [InlineData("Tests/Reserved/t1.test.ts")]
    public void NormalizeAuthorizedPath_RejectsUnauthorizedMaterial(string archivePath)
    {
        Assert.Throws<RunPreparationException>(() => WorkspacePathPolicy.NormalizeAuthorizedPath(archivePath));
    }
}
