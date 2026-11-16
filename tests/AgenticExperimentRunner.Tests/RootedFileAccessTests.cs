using System.Diagnostics;
using AgentWorkspaceMcpServer;
using Xunit;

public sealed class RootedFileAccessTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "TestResults",
        $"rooted-file-access-{Guid.NewGuid():N}");

    public RootedFileAccessTests()
    {
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(Path.Combine(rootPath, "workspace"));
        Directory.CreateDirectory(Path.Combine(rootPath, "agent-tests"));
        Directory.CreateDirectory(Path.Combine(rootPath, "public-reference"));
        File.WriteAllText(Path.Combine(rootPath, "task-specification.md"), "specifica");
        File.WriteAllText(Path.Combine(rootPath, "public-reference", "public.test.ts"), "test pubblico");
    }

    [Fact]
    public void WriteFile_CreatesAndUpdatesFileInsideRoot()
    {
        var access = CreateAccess();

        access.WriteFile(Path.Combine("workspace", "src", "example.ts"), "prima");
        access.WriteFile(Path.Combine("workspace", "src", "example.ts"), "seconda");

        Assert.Equal("seconda", access.ReadFile(Path.Combine("workspace", "src", "example.ts")));
    }

    [Fact]
    public void WriteFile_CreatesFileInAgentTests()
    {
        var access = CreateAccess();

        access.WriteFile(Path.Combine("agent-tests", "additional.test.ts"), "test aggiuntivo");

        Assert.Equal(
            "test aggiuntivo",
            access.ReadFile(Path.Combine("agent-tests", "additional.test.ts")));
    }

    [Theory]
    [InlineData("public-reference/public.test.ts")]
    [InlineData("public-reference/new.test.ts")]
    [InlineData("task-specification.md")]
    [InlineData("new-root-file.txt")]
    public void WriteFile_RejectsProtectedPaths(string relativePath)
    {
        var access = CreateAccess();

        Assert.Throws<AccessDeniedException>(() => access.WriteFile(relativePath, "negato"));
    }

    [Fact]
    public void WriteFile_RejectsParentTraversal()
    {
        var access = CreateAccess();

        Assert.Throws<AccessDeniedException>(() =>
            access.WriteFile(Path.Combine("..", "outside.txt"), "negato"));
    }

    [Fact]
    public void WriteFile_RejectsAbsolutePath()
    {
        var access = CreateAccess();
        string outsidePath = Path.Combine(
            Directory.GetParent(rootPath)!.FullName,
            $"outside-{Guid.NewGuid():N}.txt");

        Assert.Throws<AccessDeniedException>(() => access.WriteFile(outsidePath, "negato"));
    }

    [Fact]
    public void WriteFile_RejectsDirectoryLinkOutsideRoot()
    {
        string outsideDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestResults",
            $"rooted-file-access-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outsideDirectory);
        string linkPath = Path.Combine(rootPath, "workspace", "linked");

        try
        {
            CreateDirectoryLink(linkPath, outsideDirectory);
            var access = CreateAccess();

            Assert.Throws<AccessDeniedException>(() =>
                access.WriteFile(Path.Combine("workspace", "linked", "outside.txt"), "negato"));
        }
        finally
        {
            if (Directory.Exists(linkPath))
            {
                Directory.Delete(linkPath);
            }

            Directory.Delete(outsideDirectory, recursive: true);
        }
    }

    [Fact]
    public void DeleteFile_RemovesFileInsideWritableArea()
    {
        var access = CreateAccess();
        access.WriteFile(Path.Combine("workspace", "obsolete.ts"), "obsolete");

        access.DeleteFile(Path.Combine("workspace", "obsolete.ts"));

        Assert.False(File.Exists(Path.Combine(rootPath, "workspace", "obsolete.ts")));
    }

    [Theory]
    [InlineData("public-reference/public.test.ts")]
    [InlineData("task-specification.md")]
    public void DeleteFile_RejectsProtectedFile(string relativePath)
    {
        var access = CreateAccess();

        Assert.Throws<AccessDeniedException>(() => access.DeleteFile(relativePath));
    }

    [Fact]
    public void MoveFile_MovesFileBetweenWritableAreas()
    {
        var access = CreateAccess();
        access.WriteFile(Path.Combine("workspace", "source.ts"), "contenuto");

        access.MoveFile(
            Path.Combine("workspace", "source.ts"),
            Path.Combine("agent-tests", "nested", "destination.test.ts"));

        Assert.False(File.Exists(Path.Combine(rootPath, "workspace", "source.ts")));
        Assert.Equal(
            "contenuto",
            access.ReadFile(Path.Combine("agent-tests", "nested", "destination.test.ts")));
    }

    [Theory]
    [InlineData("public-reference/moved.ts")]
    [InlineData("task-specification.md")]
    public void MoveFile_RejectsProtectedDestination(string destinationPath)
    {
        var access = CreateAccess();
        access.WriteFile(Path.Combine("workspace", "source.ts"), "contenuto");

        Assert.Throws<AccessDeniedException>(() => access.MoveFile(
            Path.Combine("workspace", "source.ts"),
            destinationPath));
        Assert.True(File.Exists(Path.Combine(rootPath, "workspace", "source.ts")));
    }

    [Fact]
    public void MoveFile_RejectsExistingDestinationWithoutChangingFiles()
    {
        var access = CreateAccess();
        access.WriteFile(Path.Combine("workspace", "source.ts"), "sorgente");
        access.WriteFile(Path.Combine("workspace", "destination.ts"), "destinazione");

        Assert.Throws<IOException>(() => access.MoveFile(
            Path.Combine("workspace", "source.ts"),
            Path.Combine("workspace", "destination.ts")));
        Assert.Equal("sorgente", access.ReadFile(Path.Combine("workspace", "source.ts")));
        Assert.Equal("destinazione", access.ReadFile(Path.Combine("workspace", "destination.ts")));
    }

    private RootedFileAccess CreateAccess() => new(
        rootPath,
        new[] { "workspace", "agent-tests" });

    private static void CreateDirectoryLink(string linkPath, string targetPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("/d");
        process.StartInfo.ArgumentList.Add("/c");
        process.StartInfo.ArgumentList.Add("mklink");
        process.StartInfo.ArgumentList.Add("/J");
        process.StartInfo.ArgumentList.Add(linkPath);
        process.StartInfo.ArgumentList.Add(targetPath);
        process.Start();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"Creazione della junction non riuscita con codice {process.ExitCode}: {standardError}");
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
