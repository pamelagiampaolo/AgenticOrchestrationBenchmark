using System.Text.Json;
using AgenticExperimentRunner.SessionExecution;

/// <summary>
/// CLI finta per i test del runner: un file <c>.cmd</c> che risponde a <c>--version</c>
/// e altrimenti esegue uno script PowerShell che riproduce un copione.
/// </summary>
/// <remarks>
/// Righe del copione: JSON da emettere su stdout, oppure le direttive
/// <c>SLEEP &lt;ms&gt;</c>, <c>EXIT &lt;codice&gt;</c> e
/// <c>TOUCH &lt;percorso relativo alla cwd&gt;</c> (sovrascrive il file).
/// Lo script salva in <see cref="PromptPath"/> lo stdin ricevuto e in
/// <see cref="EnvironmentNamesPath"/> i nomi delle variabili d'ambiente.
/// </remarks>
internal sealed class FakeClaudeCli
{
    public const string Model = ExperimentProtocol.RequiredModel;

    private const string Script = """
        $ErrorActionPreference = 'Stop'
        $here = Split-Path -Parent $MyInvocation.MyCommand.Path
        $buffer = New-Object IO.MemoryStream
        [Console]::OpenStandardInput().CopyTo($buffer)
        [IO.File]::WriteAllBytes((Join-Path $here 'prompt-received.txt'), $buffer.ToArray())
        [IO.File]::WriteAllLines((Join-Path $here 'environment-names.txt'), [string[]](Get-ChildItem env: | ForEach-Object Name))
        $exitCode = 0
        foreach ($line in [IO.File]::ReadAllLines((Join-Path $here 'script.txt'))) {
            if ($line.StartsWith('SLEEP ')) { Start-Sleep -Milliseconds ([int]$line.Substring(6)); continue }
            if ($line.StartsWith('EXIT ')) { $exitCode = [int]$line.Substring(5); continue }
            if ($line.StartsWith('TOUCH ')) { [IO.File]::WriteAllText((Join-Path (Get-Location) $line.Substring(6)), 'alterato dalla sessione'); continue }
            [Console]::Out.WriteLine($line)
            [Console]::Out.Flush()
        }
        exit $exitCode
        """;

    private const string CommandFile = """
        @echo off
        if "%~1"=="--version" (
          echo 9.9.9 fake
          exit /b 0
        )
        powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0fake-claude.ps1"
        exit /b %ERRORLEVEL%
        """;

    public FakeClaudeCli(string directory, IEnumerable<string> scriptLines)
    {
        Directory.CreateDirectory(directory);
        ExecutablePath = Path.Combine(directory, "fake-claude.cmd");
        File.WriteAllText(ExecutablePath, CommandFile.Replace("\n", "\r\n"));
        File.WriteAllText(Path.Combine(directory, "fake-claude.ps1"), Script);
        File.WriteAllLines(Path.Combine(directory, "script.txt"), scriptLines);
        PromptPath = Path.Combine(directory, "prompt-received.txt");
        EnvironmentNamesPath = Path.Combine(directory, "environment-names.txt");
    }

    public string ExecutablePath { get; }

    public string PromptPath { get; }

    public string EnvironmentNamesPath { get; }

    /// <summary>Evento init conforme al profilo, con eventuali strumenti in più.</summary>
    public static string Init(params string[] extraTools) => Json(new
    {
        type = "system",
        subtype = "init",
        session_id = "sessione-finta",
        model = Model,
        claude_code_version = "9.9.9",
        permissionMode = "dontAsk",
        tools = ClaudeLaunchProfile.QualifiedMcpToolNames.Concat(extraTools).ToArray(),
        mcp_servers = new[] { new { name = ClaudeLaunchProfile.McpServerName, status = "connected" } },
        agents = Array.Empty<string>(),
        skills = Array.Empty<string>(),
        slash_commands = Array.Empty<string>(),
    });

    public static string Assistant(string messageId, long input, long cacheRead, long cacheCreation, long output, string? toolUseId = null) => Json(new
    {
        type = "assistant",
        message = new
        {
            id = messageId,
            model = Model,
            content = toolUseId is null
                ? new object[] { new { type = "text", text = "..." } }
                : new object[] { new { type = "tool_use", id = toolUseId, name = "mcp__benchmark_workspace__read_file" } },
            usage = new
            {
                input_tokens = input,
                cache_read_input_tokens = cacheRead,
                cache_creation_input_tokens = cacheCreation,
                output_tokens = output,
            },
        },
    });

    public static string ToolResult(string toolUseId, bool isError) => Json(new
    {
        type = "user",
        message = new { role = "user", content = new[] { new { type = "tool_result", tool_use_id = toolUseId, is_error = isError } } },
    });

    public static string Result(long input, long cacheRead, long cacheCreation, long output, string subtype = "success") => Json(new
    {
        type = "result",
        subtype,
        is_error = subtype != "success",
        terminal_reason = "completed",
        num_turns = 2,
        duration_ms = 1234,
        total_cost_usd = 0.01m,
        permission_denials = Array.Empty<object>(),
        usage = new
        {
            input_tokens = input,
            cache_read_input_tokens = cacheRead,
            cache_creation_input_tokens = cacheCreation,
            output_tokens = output,
        },
        modelUsage = new Dictionary<string, object> { [Model] = new { inputTokens = input } },
    });

    private static string Json(object value) => JsonSerializer.Serialize(value);
}
