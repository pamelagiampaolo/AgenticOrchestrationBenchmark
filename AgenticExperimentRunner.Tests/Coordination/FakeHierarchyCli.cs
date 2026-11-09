using System.Text.Json;
using AgenticExperimentRunner.SessionExecution;

/// <summary>
/// CLI finta per le run gerarchiche: un file <c>.cmd</c> che risponde a <c>--version</c> e
/// altrimenti esegue uno script PowerShell che riproduce un copione scelto dal prompt.
/// </summary>
/// <remarks>
/// La prima riga del prompt ricevuto su stdin, <c>SCRIPT &lt;nome&gt;</c>, sceglie il copione
/// <c>&lt;nome&gt;.txt</c>: così coordinatore (prompt = specifica) e lavoratori (prompt =
/// compito) seguono copioni diversi. Righe del copione: JSON da emettere su stdout, oppure
/// <c>SLEEP &lt;ms&gt;</c>, <c>EXIT &lt;codice&gt;</c> e <c>DELEGATE &lt;compito&gt;</c>.
/// <c>DELEGATE</c> avvia, alla prima occorrenza, il relay indicato nel file passato con
/// <c>--mcp-config</c>, esegue l'handshake MCP e invia <c>tools/call delegate_task</c>; la
/// risposta viene accodata a <see cref="DelegationResponsesPath"/>. Ogni processo salva il
/// prompt in <c>prompt-&lt;nome&gt;-&lt;pid&gt;.txt</c> e i nomi delle variabili d'ambiente in
/// <c>environment-&lt;nome&gt;-&lt;pid&gt;.txt</c>.
/// </remarks>
internal sealed class FakeHierarchyCli
{
    private const string Script = """
        $ErrorActionPreference = 'Stop'
        $here = Split-Path -Parent $MyInvocation.MyCommand.Path
        $buffer = New-Object IO.MemoryStream
        [Console]::OpenStandardInput().CopyTo($buffer)
        $bytes = $buffer.ToArray()
        $prompt = [Text.Encoding]::UTF8.GetString($bytes)
        $first = ($prompt -split "`n")[0].Trim()
        $name = 'default'
        if ($first.StartsWith('SCRIPT ')) { $name = $first.Substring(7).Trim() }
        [IO.File]::WriteAllBytes((Join-Path $here "prompt-$name-$PID.txt"), $bytes)
        [IO.File]::WriteAllLines((Join-Path $here "environment-$name-$PID.txt"), [string[]](Get-ChildItem env: | ForEach-Object Name))
        $mcpConfig = $null
        for ($i = 0; $i -lt $args.Count - 1; $i++) { if ($args[$i] -eq '--mcp-config') { $mcpConfig = $args[$i + 1] } }
        $relay = $null
        $nextId = 10
        $exitCode = 0
        foreach ($line in [IO.File]::ReadAllLines((Join-Path $here "$name.txt"))) {
            if ($line.StartsWith('SLEEP ')) { Start-Sleep -Milliseconds ([int]$line.Substring(6)); continue }
            if ($line.StartsWith('EXIT ')) { $exitCode = [int]$line.Substring(5); continue }
            if ($line.StartsWith('DELEGATE ')) {
                if ($null -eq $relay) {
                    $config = [IO.File]::ReadAllText($mcpConfig) | ConvertFrom-Json
                    $server = $config.mcpServers.benchmark_coordination
                    $startInfo = New-Object Diagnostics.ProcessStartInfo
                    $startInfo.FileName = $server.command
                    $startInfo.Arguments = (($server.args | ForEach-Object { '"' + $_ + '"' }) -join ' ')
                    $startInfo.UseShellExecute = $false
                    $startInfo.RedirectStandardInput = $true
                    $startInfo.RedirectStandardOutput = $true
                    $relay = [Diagnostics.Process]::Start($startInfo)
                    $relay.StandardInput.WriteLine('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}')
                    $relay.StandardInput.Flush()
                    [void]$relay.StandardOutput.ReadLine()
                    $relay.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
                    $relay.StandardInput.Flush()
                }
                $task = ConvertTo-Json $line.Substring(9)
                $relay.StandardInput.WriteLine('{"jsonrpc":"2.0","id":' + $nextId + ',"method":"tools/call","params":{"name":"delegate_task","arguments":{"task":' + $task + '}}}')
                $relay.StandardInput.Flush()
                $nextId++
                $response = $relay.StandardOutput.ReadLine()
                [IO.File]::AppendAllText((Join-Path $here 'delegation-responses.txt'), $response + "`n")
                continue
            }
            [Console]::Out.WriteLine($line)
            [Console]::Out.Flush()
        }
        if ($null -ne $relay) { $relay.StandardInput.Close(); $relay.WaitForExit() }
        exit $exitCode
        """;

    private const string CommandFile = """
        @echo off
        if "%~1"=="--version" (
          echo 9.9.9 fake
          exit /b 0
        )
        powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0fake-claude.ps1" %*
        exit /b %ERRORLEVEL%
        """;

    public FakeHierarchyCli(string directory, IReadOnlyDictionary<string, IEnumerable<string>> scripts)
    {
        Directory.CreateDirectory(directory);
        DirectoryPath = directory;
        ExecutablePath = Path.Combine(directory, "fake-claude.cmd");
        File.WriteAllText(ExecutablePath, CommandFile.Replace("\n", "\r\n"));
        File.WriteAllText(Path.Combine(directory, "fake-claude.ps1"), Script);
        foreach ((string name, IEnumerable<string> lines) in scripts)
        {
            File.WriteAllLines(Path.Combine(directory, $"{name}.txt"), lines);
        }

        DelegationResponsesPath = Path.Combine(directory, "delegation-responses.txt");
    }

    public string DirectoryPath { get; }

    public string ExecutablePath { get; }

    public string DelegationResponsesPath { get; }

    /// <summary>Risposte JSON-RPC ricevute dal coordinatore finto, nell'ordine.</summary>
    public IReadOnlyList<JsonElement> DelegationResponses() =>
        File.Exists(DelegationResponsesPath)
            ? File.ReadAllLines(DelegationResponsesPath)
                .Where(line => line.Length > 0)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .ToList()
            : [];

    /// <summary>Prompt ricevuti dai processi che hanno eseguito il copione indicato.</summary>
    public IReadOnlyList<string> PromptsOf(string scriptName) =>
        Directory.GetFiles(DirectoryPath, $"prompt-{scriptName}-*.txt")
            .Select(path => File.ReadAllText(path))
            .ToList();

    /// <summary>Nomi delle variabili d'ambiente visti dai processi di tutti i copioni.</summary>
    public IReadOnlyList<string> AllEnvironmentNames() =>
        Directory.GetFiles(DirectoryPath, "environment-*.txt").SelectMany(File.ReadAllLines).ToList();

    /// <summary>Evento init del coordinatore: strumenti del workspace, delega e due server.</summary>
    public static string CoordinatorInit() => Json(new
    {
        type = "system",
        subtype = "init",
        session_id = "coordinatore-finto",
        model = FakeClaudeCli.Model,
        claude_code_version = "9.9.9",
        permissionMode = "dontAsk",
        tools = ClaudeLaunchProfile.QualifiedMcpToolNames.Append(ClaudeLaunchProfile.QualifiedDelegateToolName).ToArray(),
        mcp_servers = new[]
        {
            new { name = ClaudeLaunchProfile.McpServerName, status = "connected" },
            new { name = ClaudeLaunchProfile.CoordinationServerName, status = "connected" },
        },
        agents = Array.Empty<string>(),
        skills = Array.Empty<string>(),
        slash_commands = Array.Empty<string>(),
    });

    /// <summary>Evento <c>result</c> di successo con testo finale e uso.</summary>
    public static string Result(string text, long input, long cacheRead, long cacheCreation, long output) => Json(new
    {
        type = "result",
        subtype = "success",
        is_error = false,
        result = text,
        num_turns = 1,
        duration_ms = 100,
        usage = new
        {
            input_tokens = input,
            cache_read_input_tokens = cacheRead,
            cache_creation_input_tokens = cacheCreation,
            output_tokens = output,
        },
    });

    private static string Json(object value) => JsonSerializer.Serialize(value);
}
