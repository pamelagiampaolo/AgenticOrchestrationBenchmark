using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class ToolchainIdentityTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"a6-toolchain-{Guid.NewGuid():N}");

    public ToolchainIdentityTests() => Directory.CreateDirectory(root);

    public void Dispose() => TestDirectories.DeleteRecursively(root);

    [Fact]
    public async Task InspectAsync_RecordsNodeVersionHashesAndPackageVersions()
    {
        string node = WriteNode("v24.17.0", exitCode: 0);
        string vitest = WritePackageFile("node_modules/vitest", "vitest", "5.1.0", "vitest.mjs", "// vitest\n");
        string typescript = WritePackageFile("node_modules/typescript", "typescript", "7.0.2", "lib/tsc.js", "// tsc\n");

        ToolchainIdentity identity = await ToolchainIdentity.InspectAsync(
            new McpDevelopmentToolchain(node, vitest, typescript), root, TestContext.Current.CancellationToken);

        Assert.Equal("v24.17.0", identity.NodeVersion);
        Assert.Equal("5.1.0", identity.VitestVersion);
        Assert.Equal("7.0.2", identity.TypeScriptVersion);
        Assert.Equal(RunInputs.Sha256(File.ReadAllBytes(node)), identity.NodeExecutableSha256);
        Assert.Equal(RunInputs.Sha256(File.ReadAllBytes(vitest)), identity.VitestEntrySha256);
        Assert.Equal(RunInputs.Sha256(File.ReadAllBytes(typescript)), identity.TypeScriptEntrySha256);
        Assert.Equal(typescript, identity.TypeScriptPath);
    }

    [Fact]
    public async Task InspectAsync_NodeVersionFailure_PreventsTheRun()
    {
        string node = WriteNode("", exitCode: 4);
        string file = WritePackageFile("pkg", "vitest", "1.0.0", "vitest.mjs", "x");

        SessionExecutionException exception = await Assert.ThrowsAsync<SessionExecutionException>(() =>
            ToolchainIdentity.InspectAsync(new McpDevelopmentToolchain(node, file, file), root, TestContext.Current.CancellationToken));

        Assert.Contains("--version", exception.Message);
    }

    [Fact]
    public void FindPackageVersion_SkipsPackagesWithOtherNamesAndInvalidManifests()
    {
        // Il pacchetto interno ha un altro nome e un package.json illeggibile sta in mezzo:
        // vale il primo package.json risalendo con il nome atteso.
        string entry = WritePackageFile("node_modules/typescript", "typescript", "7.0.2", "lib/sub/tsc.js", "x");
        File.WriteAllText(Path.Combine(root, "node_modules", "typescript", "lib", "package.json"), """{"name":"altro","version":"9.9.9"}""");
        File.WriteAllText(Path.Combine(root, "node_modules", "typescript", "lib", "sub", "package.json"), "{ non json");

        Assert.Equal("7.0.2", ToolchainIdentity.FindPackageVersion(entry, "typescript"));
    }

    [Fact]
    public void FindPackageVersion_PackageNotFound_ReturnsNull()
    {
        string file = Path.Combine(root, "isolato.js");
        File.WriteAllText(file, "x");

        Assert.Null(ToolchainIdentity.FindPackageVersion(file, "vitest"));
    }

    private string WriteNode(string version, int exitCode)
    {
        string path = Path.Combine(root, $"node-{Guid.NewGuid():N}.cmd");
        string echo = version.Length > 0 ? $"echo {version}" : "rem nessuna versione";
        File.WriteAllText(path, $"@echo off\r\n{echo}\r\nexit /b {exitCode}\r\n");
        return path;
    }

    private string WritePackageFile(string packageDirectory, string name, string version, string relativeFile, string content)
    {
        string directory = Path.Combine(root, packageDirectory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "package.json"), $$"""{"name":"{{name}}","version":"{{version}}"}""");
        string file = Path.Combine(directory, relativeFile.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }
}
