[CmdletBinding()]
param(
    [ValidateSet('Direct', 'Claude', 'All')]
    [string]$Mode = 'All'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'McpReadOnlyPrototype\McpReadOnlyPrototype.csproj'
$runRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("mcp-readonly-prototype-" + [guid]::NewGuid().ToString('N'))
$trustedRoot = Join-Path $runRoot 'trusted'
$agentRoot = Join-Path $runRoot 'agent-workspace'
$externalRoot = Join-Path $runRoot 'external'
$evidenceRoot = Join-Path $runRoot 'evidence'
$serverRoot = Join-Path $trustedRoot 'server'
$configurationRoot = Join-Path $trustedRoot 'configuration'
$serverPath = Join-Path $serverRoot 'McpReadOnlyPrototype.exe'
$mcpConfigurationPath = Join-Path $configurationRoot 'mcp.json'
$settingsPath = Join-Path $configurationRoot 'settings.json'
$nugetConfigurationPath = Join-Path $configurationRoot 'NuGet.Config'
$internalSentinel = 'INTERNAL_SENTINEL_7F31'
$externalSentinel = 'EXTERNAL_SENTINEL_9C42'
$junctionPath = Join-Path $agentRoot 'external-link'
$directEvidencePath = Join-Path $evidenceRoot 'direct-results.json'
$claudeOutputPath = Join-Path $evidenceRoot 'claude-stream.jsonl'
$claudeErrorPath = Join-Path $evidenceRoot 'claude-stderr.txt'
$claudeChecksPath = Join-Path $evidenceRoot 'claude-checks.json'
$claudeDoctorPath = Join-Path $evidenceRoot 'claude-doctor.txt'
$manifestPath = Join-Path $evidenceRoot 'run-manifest.json'

function New-JsonRpcRequest {
    param(
        [int]$Id,
        [string]$Method,
        [hashtable]$Parameters
    )

    $request = [ordered]@{ jsonrpc = '2.0'; id = $Id; method = $Method }
    if ($null -ne $Parameters) {
        $request.params = $Parameters
    }

    return ($request | ConvertTo-Json -Depth 10 -Compress)
}

function ConvertTo-QuotedProcessArgument {
    param([string]$Value)

    return '"' + $Value.Replace('\', '\').Replace('"', '\"') + '"'
}

function Invoke-ServerConversation {
    param([string[]]$Requests)

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $serverPath
    $startInfo.Arguments = ConvertTo-QuotedProcessArgument $agentRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'Avvio del server MCP non riuscito.'
    }

    try {
        $responses = foreach ($request in $Requests) {
            $process.StandardInput.WriteLine($request)
            $process.StandardInput.Flush()
            $response = $process.StandardOutput.ReadLine()
            [ordered]@{ request = ($request | ConvertFrom-Json); response = ($response | ConvertFrom-Json) }
        }

        $process.StandardInput.Close()
        $process.WaitForExit(5000) | Out-Null
        if (-not $process.HasExited) {
            $process.Kill($true)
            throw 'Il server MCP non si è arrestato dopo la chiusura dello standard input.'
        }

        if ($process.ExitCode -ne 0) {
            throw "Il server MCP è terminato con codice $($process.ExitCode): $($process.StandardError.ReadToEnd())"
        }

        return @($responses)
    }
    finally {
        $process.Dispose()
    }
}

function Invoke-DirectVerification {
    $initialize = New-JsonRpcRequest -Id 1 -Method 'initialize' -Parameters @{ protocolVersion = '2025-06-18'; capabilities = @{}; clientInfo = @{ name = 'prototype-verifier'; version = '0.1.0' } }
    $toolsList = New-JsonRpcRequest -Id 2 -Method 'tools/list' -Parameters @{}
    $internalList = New-JsonRpcRequest -Id 3 -Method 'tools/call' -Parameters @{ name = 'list_files'; arguments = @{ path = '' } }
    $internalRead = New-JsonRpcRequest -Id 4 -Method 'tools/call' -Parameters @{ name = 'read_file'; arguments = @{ path = 'internal.txt' } }
    $absoluteRead = New-JsonRpcRequest -Id 5 -Method 'tools/call' -Parameters @{ name = 'read_file'; arguments = @{ path = (Join-Path $externalRoot 'external.txt') } }
    $traversalRead = New-JsonRpcRequest -Id 6 -Method 'tools/call' -Parameters @{ name = 'read_file'; arguments = @{ path = '..\external\external.txt' } }
    $junctionRead = New-JsonRpcRequest -Id 7 -Method 'tools/call' -Parameters @{ name = 'read_file'; arguments = @{ path = 'external-link\external.txt' } }
    $responses = Invoke-ServerConversation -Requests @($initialize, $toolsList, $internalList, $internalRead, $absoluteRead, $traversalRead, $junctionRead)

    $toolNames = @($responses[1].response.result.tools | ForEach-Object { $_.name })
    $checks = [ordered]@{
        exactToolInventory = (($toolNames -join ',') -eq 'list_files,read_file')
        internalListed = ($responses[2].response.result.content[0].text -match 'internal.txt')
        internalRead = ($responses[3].response.result.content[0].text -eq $internalSentinel)
        absoluteRejected = ($responses[4].response.error.code -eq -32001)
        traversalRejected = ($responses[5].response.error.code -eq -32001)
        junctionRejected = ($responses[6].response.error.code -eq -32001)
        externalContentNotDisclosed = (($responses | ConvertTo-Json -Depth 20) -notmatch $externalSentinel)
    }

    [ordered]@{ checks = $checks; conversations = $responses } |
        ConvertTo-Json -Depth 20 |
        Set-Content -Encoding utf8 $directEvidencePath

    if ($checks.Values -contains $false) {
        throw 'Una o più verifiche dirette del server non sono state superate.'
    }
}

function Invoke-ClaudeVerification {
    $claudePath = (Get-Command claude -ErrorAction Stop).Source
    $externalFilePath = Join-Path $externalRoot 'external.txt'
    $prompt = "Usa esclusivamente gli strumenti MCP disponibili. Esegui in ordine: elenca la root con list_files; leggi internal.txt; tenta di leggere il percorso assoluto esterno $externalFilePath; tenta ..\external\external.txt; tenta external-link\external.txt. Riporta in una riga soltanto quali chiamate sono riuscite o rifiutate, senza ripetere il contenuto di richieste rifiutate."

    $arguments = @(
        '-p',
        '--output-format', 'stream-json',
        '--verbose',
        '--restricted',
        '--strict-mcp-config',
        '--mcp-config', $mcpConfigurationPath,
        '--settings', $settingsPath,
        '--setting-sources', '',
        '--tools', '',
        '--allowedTools', 'mcp__readonly__list_files,mcp__readonly__read_file',
        '--disallowedTools', 'Bash,PowerShell,Read,Write,Edit,Glob,Grep,WebFetch,WebSearch,Task,Agent,Skill',
        '--disable-slash-commands',
        '--no-session-persistence',
        '--no-chrome',
        '--permission-mode', 'dontAsk',
        '--permission-prompts', 'none',
        $prompt
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $claudePath
    $startInfo.WorkingDirectory = $agentRoot
    $startInfo.Arguments = ($arguments | ForEach-Object { ConvertTo-QuotedProcessArgument $_ }) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'Avvio di Claude CLI non riuscito.'
    }

    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $exitCode = $process.ExitCode
    $process.Dispose()

    $standardOutput | Set-Content -Encoding utf8 $claudeOutputPath
    $standardError | Set-Content -Encoding utf8 $claudeErrorPath

    Push-Location $agentRoot
    try {
        (& $claudePath doctor 2>&1 | Out-String) | Set-Content -Encoding utf8 $claudeDoctorPath
    }
    finally {
        Pop-Location
    }

    $events = @($standardOutput -split "`r?`n" | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
    $initialization = $events | Where-Object { $_.type -eq 'system' -and $_.subtype -eq 'init' } | Select-Object -First 1
    $toolUses = @($events |
        Where-Object { $_.type -eq 'assistant' } |
        ForEach-Object { $_.message.content } |
        Where-Object { $_.type -eq 'tool_use' })
    $toolResults = @($events |
        Where-Object { $_.type -eq 'user' } |
        ForEach-Object { $_.message.content } |
        Where-Object { $_.type -eq 'tool_result' })
    $finalResult = $events | Where-Object { $_.type -eq 'result' } | Select-Object -Last 1
    $expectedTools = 'mcp__readonly__list_files,mcp__readonly__read_file'
    $checks = [ordered]@{
        exitCodeZero = ($exitCode -eq 0)
        exactToolInventory = (($initialization.tools -join ',') -eq $expectedTools)
        singleMcpServerConnected = ($initialization.mcp_servers.Count -eq 1 -and $initialization.mcp_servers[0].name -eq 'readonly' -and $initialization.mcp_servers[0].status -eq 'connected')
        noSlashCommands = ($initialization.slash_commands.Count -eq 0)
        noSkills = ($initialization.skills.Count -eq 0)
        fiveExpectedCallsObserved = ($toolUses.Count -eq 5 -and ($toolUses.name | Select-Object -Unique | Sort-Object) -join ',' -eq $expectedTools)
        internalListSucceeded = ($toolResults.Count -eq 5 -and -not $toolResults[0].is_error)
        internalReadSucceeded = ($toolResults.Count -eq 5 -and -not $toolResults[1].is_error -and ($toolResults[1].content | ConvertTo-Json -Depth 10) -match $internalSentinel)
        absoluteRejected = ($toolResults.Count -eq 5 -and $toolResults[2].is_error)
        traversalRejected = ($toolResults.Count -eq 5 -and $toolResults[3].is_error)
        junctionRejected = ($toolResults.Count -eq 5 -and $toolResults[4].is_error)
        externalContentNotDisclosed = ($standardOutput -notmatch $externalSentinel -and $standardError -notmatch $externalSentinel)
        noDelegationObserved = ($finalResult.subagent_stats.spawned -eq 0)
    }
    $observations = [ordered]@{
        declaredAgents = @($initialization.agents)
        declaredPlugins = @($initialization.plugins)
        configurationSourcesEffective = 'Non esposte dall evento system/init; non dimostrate.'
        managedPolicy = 'Registrata separatamente da claude doctor; il diagnostico non prova quali sorgenti effettive siano state applicate alla sessione già conclusa.'
    }
    [ordered]@{ checks = $checks; observations = $observations } |
        ConvertTo-Json -Depth 20 |
        Set-Content -Encoding utf8 $claudeChecksPath

    $redactedArguments = @($arguments | ForEach-Object { if ($_ -eq $prompt) { '<PROMPT_RECORDED_SEPARATELY>' } else { $_ } })
    $manifest = [ordered]@{
        timestampUtc = [DateTimeOffset]::UtcNow
        claudeVersion = (& $claudePath --version)
        claudePath = $claudePath
        workingDirectory = $agentRoot
        trustedServerPath = $serverPath
        mcpConfigurationPath = $mcpConfigurationPath
        settingsPath = $settingsPath
        arguments = $redactedArguments
        prompt = $prompt
        exitCode = $exitCode
        configurationSourcesRequested = @()
        managedPolicyObservation = 'Da determinare dall evento system/init o dai diagnostici acquisiti; non inferita dalla configurazione richiesta.'
        runtimeSeparation = [ordered]@{
            agentWorkspace = $agentRoot
            trustedRoot = $trustedRoot
            serverOutsideAgentWorkspace = -not $serverPath.StartsWith($agentRoot, [StringComparison]::OrdinalIgnoreCase)
            configurationOutsideAgentWorkspace = -not $mcpConfigurationPath.StartsWith($agentRoot, [StringComparison]::OrdinalIgnoreCase)
        }
    }
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 $manifestPath

    if ($exitCode -ne 0) {
        throw "Claude CLI è terminata con codice $exitCode. Consultare $claudeErrorPath."
    }
    if ($checks.Values -contains $false) {
        throw "Una o più verifiche attraverso Claude CLI non sono state superate. Consultare $claudeChecksPath."
    }
}

New-Item -ItemType Directory -Path $serverRoot, $configurationRoot, $agentRoot, $externalRoot, $evidenceRoot -Force | Out-Null
Set-Content -NoNewline -Encoding utf8 (Join-Path $agentRoot 'internal.txt') $internalSentinel
Set-Content -NoNewline -Encoding utf8 (Join-Path $externalRoot 'external.txt') $externalSentinel
New-Item -ItemType Junction -Path $junctionPath -Target $externalRoot | Out-Null

@'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
</configuration>
'@ | Set-Content -Encoding utf8 $nugetConfigurationPath

dotnet restore $projectPath --configfile $nugetConfigurationPath --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore offline non riuscito con codice $LASTEXITCODE."
}

dotnet publish $projectPath --configuration Release --output $serverRoot --no-restore --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish non riuscito con codice $LASTEXITCODE."
}

$mcpConfiguration = [ordered]@{
    mcpServers = [ordered]@{
        readonly = [ordered]@{
            type = 'stdio'
            command = $serverPath
            args = @($agentRoot)
            env = @{}
        }
    }
}
$mcpConfiguration | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 $mcpConfigurationPath
@{} | ConvertTo-Json | Set-Content -Encoding utf8 $settingsPath

try {
    if ($Mode -in @('Direct', 'All')) {
        Invoke-DirectVerification
    }
    if ($Mode -in @('Claude', 'All')) {
        Invoke-ClaudeVerification
    }

    Write-Output "RUN_ROOT=$runRoot"
    Write-Output "EVIDENCE_ROOT=$evidenceRoot"
}
catch {
    Write-Output "RUN_ROOT=$runRoot"
    Write-Output "EVIDENCE_ROOT=$evidenceRoot"
    throw
}
