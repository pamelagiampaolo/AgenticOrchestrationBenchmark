using System.Diagnostics;
using System.Text;
using System.Text.Json;
using McpReadOnlyPrototype;
using Xunit;

namespace AgenticExperimentRunner.Tests;

public sealed class McpServerStartupTests
{
    [Fact]
    public async Task StdioTransport_PreservesUtf8ToolArguments()
    {
        string root = Path.Combine(
            Path.GetFullPath("TestResults"),
            $"mcp-utf8-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "workspace"));
        try
        {
            string server = Path.Combine(AppContext.BaseDirectory, "McpReadOnlyPrototype.dll");
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = new UTF8Encoding(false),
                    StandardOutputEncoding = new UTF8Encoding(false, true),
                    StandardErrorEncoding = new UTF8Encoding(false, true),
                    CreateNoWindow = true,
                },
            };
            process.StartInfo.ArgumentList.Add(server);
            process.StartInfo.ArgumentList.Add(root);
            process.StartInfo.Environment["MCP_WRITABLE_DIRECTORIES"] = "workspace";
            Assert.True(process.Start());

            string content = "quantità, perché, è così";
            string writeRequest = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new
                {
                    name = "write_file",
                    arguments = new { path = "workspace/utf8.txt", content },
                },
            });
            await process.StandardInput.WriteLineAsync(writeRequest);
            await process.StandardInput.FlushAsync(TestContext.Current.CancellationToken);
            process.StandardInput.Close();

            string output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            string error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);

            Assert.True(process.ExitCode == 0, error);
            Assert.Equal(string.Empty, error);
            using JsonDocument response = JsonDocument.Parse(output);
            Assert.False(response.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.Equal(content, await File.ReadAllTextAsync(
                Path.Combine(root, "workspace", "utf8.txt"),
                Encoding.UTF8,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            TestDirectories.DeleteRecursively(root);
        }
    }

    [Fact]
    public void ConfigurationPolicy_HandlesUnauthorizedAccess()
    {
        Assert.True(StartupExceptionPolicy.IsConfigurationError(
            new UnauthorizedAccessException("accesso negato")));
    }

    [Fact]
    public void ConfigurationPolicy_DoesNotHideUnexpectedFailures()
    {
        Assert.False(StartupExceptionPolicy.IsConfigurationError(
            new InvalidOperationException("errore inatteso")));
    }
}
