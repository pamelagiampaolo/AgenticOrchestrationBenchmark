using System.Diagnostics;
using McpReadOnlyPrototype;
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
    }

    [Fact]
    public void WriteFile_CreatesAndUpdatesFileInsideRoot()
    {
        var access = new RootedFileAccess(rootPath);

        access.WriteFile(Path.Combine("src", "example.ts"), "prima");
        access.WriteFile(Path.Combine("src", "example.ts"), "seconda");

        Assert.Equal("seconda", access.ReadFile(Path.Combine("src", "example.ts")));
    }

    [Fact]
    public void WriteFile_RejectsParentTraversal()
    {
        var access = new RootedFileAccess(rootPath);

        Assert.Throws<AccessDeniedException>(() =>
            access.WriteFile(Path.Combine("..", "outside.txt"), "negato"));
    }

    [Fact]
    public void WriteFile_RejectsAbsolutePath()
    {
        var access = new RootedFileAccess(rootPath);
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
        string linkPath = Path.Combine(rootPath, "linked");

        try
        {
            CreateDirectoryLink(linkPath, outsideDirectory);
            var access = new RootedFileAccess(rootPath);

            Assert.Throws<AccessDeniedException>(() =>
                access.WriteFile(Path.Combine("linked", "outside.txt"), "negato"));
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
