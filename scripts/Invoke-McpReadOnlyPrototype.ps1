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
$junctionPath = Join-Path $agentRoot 'workspace\external-link'
$directEvidencePath = Join-Path $evidenceRoot 'direct-results.json'
$claudeOutputPath = Join-Path $evidenceRoot 'claude-stream.jsonl'
$claudeErrorPath = Join-Path $evidenceRoot 'claude-stderr.txt'
$claudeChecksPath = Join-Path $evidenceRoot 'claude-checks.json'
$claudeDoctorPath = Join-Path $evidenceRoot 'claude-doctor.txt'
$manifestPath = Join-Path $evidenceRoot 'run-manifest.json'
$projectHookSentinelPath = Join-Path $evidenceRoot 'project-hook-fired.txt'
$trustedHookSentinelPath = Join-Path $evidenceRoot 'trusted-hook-fired.txt'
$hookControlSentinelPath = Join-Path $evidenceRoot 'hook-positive-control-fired.txt'
$hookControlSettingsPath = Join-Path $configurationRoot 'hook-positive-control-settings.json'
$hookControlOutputPath = Join-Path $evidenceRoot 'hook-positive-control-stream.jsonl'
$projectClaudeRoot = Join-Path $agentRoot '.claude'
$projectSettingsPath = Join-Path $projectClaudeRoot 'settings.json'
$projectHookPath = Join-Path $projectClaudeRoot 'project-hook.ps1'
$trustedHookPath = Join-Path $configurationRoot 'trusted-hook.ps1'

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

function Set-Utf8WithoutBom {
    param(
        [string]$LiteralPath,
        [string]$Value
    )

    [IO.File]::WriteAllText($LiteralPath, $Value, [Text.UTF8Encoding]::new($false))
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
    $startInfo.EnvironmentVariables['MCP_WRITABLE_DIRECTORIES'] = 'workspace;agent-tests'
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
    $internalRead = New-JsonRpcRequest -Id 4 -Method 'tools/call' -Parameters @{ name = 'read_file'; arguments = @{ path = 'task-specification.md' } }
    $publicRead = New-JsonRpcRequest -Id 5 -Method 'tools/call' -Parameters @{ name = 'read_file'; arguments = @{ path = 'public-reference\public.txt' } }
    $workspaceWrite = New-JsonRpcRequest -Id 6 -Method 'tools/call' -Parameters @{ name = 'write_file'; arguments = @{ path = 'workspace\generated\result.txt'; content = 'WRITTEN_WORKSPACE' } }
    $agentTestWrite = New-JsonRpcRequest -Id 7 -Method 'tools/call' -Parameters @{ name = 'write_file'; arguments = @{ path = 'agent-tests\generated.test.ts'; content = 'WRITTEN_AGENT_TEST' } }
    $publicWrite = New-JsonRpcRequest -Id 8 -Method 'tools/call' -Parameters @{ name = 'write_file'; arguments = @{ path = 'public-reference\blocked.txt'; content = 'BLOCKED' } }
    $specificationWrite = New-JsonRpcRequest -Id 9 -Method 'tools/call' -Parameters @{ name = 'write_file'; arguments = @{ path = 'task-specification.md'; content = 'BLOCKED' } }
    $absoluteRead = New-JsonRpcRequest -Id 10 -Method 'tools/call' -Parameters @{ name = 'read_file'; arguments = @{ path = (Join-Path $externalRoot 'external.txt') } }
    $traversalRead = New-JsonRpcRequest -Id 11 -Method 'tools/call' -Parameters @{ name = 'read_file'; arguments = @{ path = '..\external\external.txt' } }
    $junctionRead = New-JsonRpcRequest -Id 12 -Method 'tools/call' -Parameters @{ name = 'read_file'; arguments = @{ path = 'workspace\external-link\external.txt' } }
    $traversalWrite = New-JsonRpcRequest -Id 13 -Method 'tools/call' -Parameters @{ name = 'write_file'; arguments = @{ path = '..\external\blocked.txt'; content = 'BLOCKED' } }
    $junctionWrite = New-JsonRpcRequest -Id 14 -Method 'tools/call' -Parameters @{ name = 'write_file'; arguments = @{ path = 'workspace\external-link\blocked.txt'; content = 'BLOCKED' } }
    $responses = Invoke-ServerConversation -Requests @(
        $initialize, $toolsList, $internalList, $internalRead, $publicRead,
        $workspaceWrite, $agentTestWrite, $publicWrite, $specificationWrite,
        $absoluteRead, $traversalRead, $junctionRead, $traversalWrite, $junctionWrite)

    $toolNames = @($responses[1].response.result.tools | ForEach-Object { $_.name })
    $checks = [ordered]@{
        exactToolInventory = (($toolNames -join ',') -eq 'list_files,read_file,write_file')
        internalListed = ($responses[2].response.result.content[0].text -match 'task-specification.md')
        internalRead = ($responses[3].response.result.content[0].text -eq $internalSentinel)
        publicRead = ($responses[4].response.result.content[0].text -eq 'PUBLIC_REFERENCE')
        workspaceWrite = ($responses[5].response.result.isError -eq $false -and (Get-Content -Raw -LiteralPath (Join-Path $agentRoot 'workspace\generated\result.txt')) -eq 'WRITTEN_WORKSPACE')
        agentTestWrite = ($responses[6].response.result.isError -eq $false -and (Get-Content -Raw -LiteralPath (Join-Path $agentRoot 'agent-tests\generated.test.ts')) -eq 'WRITTEN_AGENT_TEST')
        publicWriteRejected = ($responses[7].response.error.code -eq -32001)
        specificationWriteRejected = ($responses[8].response.error.code -eq -32001)
        absoluteRejected = ($responses[9].response.error.code -eq -32001)
        traversalRejected = ($responses[10].response.error.code -eq -32001)
        junctionRejected = ($responses[11].response.error.code -eq -32001)
        traversalWriteRejected = ($responses[12].response.error.code -eq -32001)
        junctionWriteRejected = ($responses[13].response.error.code -eq -32001)
        protectedContentUnchanged = ((Get-Content -Raw -LiteralPath (Join-Path $agentRoot 'task-specification.md')) -eq $internalSentinel -and (Get-Content -Raw -LiteralPath (Join-Path $agentRoot 'public-reference\public.txt')) -eq 'PUBLIC_REFERENCE')
        externalFileNotWritten = (-not (Test-Path -LiteralPath (Join-Path $externalRoot 'blocked.txt')))
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
    $prompt = "Usa esclusivamente gli strumenti MCP disponibili. Esegui esattamente in ordine: elenca la root; leggi task-specification.md; leggi public-reference\public.txt; scrivi GENERATED_WORKSPACE in workspace\generated\claude.txt; scrivi GENERATED_TEST in agent-tests\claude.test.ts; tenta di scrivere BLOCKED in public-reference\blocked.txt; tenta di scrivere BLOCKED in task-specification.md; tenta di leggere il percorso assoluto esterno $externalFilePath; tenta di leggere ..\external\external.txt e workspace\external-link\external.txt; tenta di scrivere BLOCKED in ..\external\blocked.txt e workspace\external-link\blocked.txt. Riporta in una riga soltanto quali chiamate sono riuscite o rifiutate, senza ripetere contenuti esterni."

    $arguments = @(
        '-p',
        '--output-format', 'stream-json',
        '--verbose',
        '--include-hook-events',
        '--restricted',
        '--mcp-config', $mcpConfigurationPath,
        '--settings', $settingsPath,
        '--setting-sources', '',
        '--tools', '',
        '--allowedTools', 'mcp__benchmark_workspace__list_files,mcp__benchmark_workspace__read_file,mcp__benchmark_workspace__write_file',
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
    $hookEvents = @($events | Where-Object { $_.type -eq 'system' -and $_.subtype -match 'hook' })
    $expectedTools = @(
        'mcp__benchmark_workspace__list_files',
        'mcp__benchmark_workspace__read_file',
        'mcp__benchmark_workspace__write_file')
    $checks = [ordered]@{
        exitCodeZero = ($exitCode -eq 0)
        exactToolInventory = ((@($initialization.tools | Sort-Object) -join ',') -eq (@($expectedTools | Sort-Object) -join ','))
        singleMcpServerConnected = ($initialization.mcp_servers.Count -eq 1 -and $initialization.mcp_servers[0].name -eq 'benchmark_workspace' -and $initialization.mcp_servers[0].status -eq 'connected')
        noMcpServerErrors = (@(
            $initialization.mcp_server_errors |
                Where-Object { $null -ne $_ }
            ).Count -eq 0)
        noSlashCommands = ($initialization.slash_commands.Count -eq 0)
        noSkills = ($initialization.skills.Count -eq 0)
        expectedCallsObserved = ($toolUses.Count -eq 12 -and (@($toolUses.name | Select-Object -Unique | Sort-Object) -join ',') -eq (@($expectedTools | Sort-Object) -join ','))
        internalListSucceeded = ($toolResults.Count -eq 12 -and -not $toolResults[0].is_error)
        internalReadSucceeded = ($toolResults.Count -eq 12 -and -not $toolResults[1].is_error -and ($toolResults[1].content | ConvertTo-Json -Depth 10) -match $internalSentinel)
        publicReadSucceeded = ($toolResults.Count -eq 12 -and -not $toolResults[2].is_error -and ($toolResults[2].content | ConvertTo-Json -Depth 10) -match 'PUBLIC_REFERENCE')
        workspaceWriteSucceeded = ($toolResults.Count -eq 12 -and -not $toolResults[3].is_error -and (Get-Content -Raw -LiteralPath (Join-Path $agentRoot 'workspace\generated\claude.txt')) -eq 'GENERATED_WORKSPACE')
        agentTestWriteSucceeded = ($toolResults.Count -eq 12 -and -not $toolResults[4].is_error -and (Get-Content -Raw -LiteralPath (Join-Path $agentRoot 'agent-tests\claude.test.ts')) -eq 'GENERATED_TEST')
        publicWriteRejected = ($toolResults.Count -eq 12 -and $toolResults[5].is_error)
        specificationWriteRejected = ($toolResults.Count -eq 12 -and $toolResults[6].is_error)
        absoluteRejected = ($toolResults.Count -eq 12 -and $toolResults[7].is_error)
        traversalRejected = ($toolResults.Count -eq 12 -and $toolResults[8].is_error)
        junctionRejected = ($toolResults.Count -eq 12 -and $toolResults[9].is_error)
        traversalWriteRejected = ($toolResults.Count -eq 12 -and $toolResults[10].is_error)
        junctionWriteRejected = ($toolResults.Count -eq 12 -and $toolResults[11].is_error)
        protectedContentUnchanged = ((Get-Content -Raw -LiteralPath (Join-Path $agentRoot 'task-specification.md')) -eq $internalSentinel -and (Get-Content -Raw -LiteralPath (Join-Path $agentRoot 'public-reference\public.txt')) -eq 'PUBLIC_REFERENCE')
        externalFileNotWritten = (-not (Test-Path -LiteralPath (Join-Path $externalRoot 'blocked.txt')))
        externalContentNotDisclosed = ($standardOutput -notmatch $externalSentinel -and $standardError -notmatch $externalSentinel)
        noDelegationObserved = ($finalResult.subagent_stats.spawned -eq 0)
        builtInSubagentsRemoved = (($initialization.agents | Where-Object { $_ -ne 'claude' }).Count -eq 0)
        noHookEventsObserved = ($hookEvents.Count -eq 0)
        projectHookDidNotExecute = (-not (Test-Path $projectHookSentinelPath))
        trustedHookDidNotExecute = (-not (Test-Path $trustedHookSentinelPath))
    }
    $observations = [ordered]@{
        declaredAgents = @($initialization.agents)
        declaredPlugins = @($initialization.plugins)
        mcpServerErrors = @(
            $initialization.mcp_server_errors |
                Where-Object { $null -ne $_ })
        configurationSourcesEffective = 'Non esposte dall evento system/init; non dimostrate.'
        managedPolicy = 'Registrata separatamente da claude doctor; il diagnostico non prova quali sorgenti effettive siano state applicate alla sessione già conclusa.'
        hookEvents = $hookEvents
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

function Invoke-HookPositiveControl {
    $claudePath = (Get-Command claude -ErrorAction Stop).Source
    $controlArguments = @(
        '-p',
        '--output-format', 'stream-json',
        '--verbose',
        '--include-hook-events',
        '--restricted',
        '--strict-mcp-config',
        '--mcp-config', $mcpConfigurationPath,
        '--settings', $hookControlSettingsPath,
        '--setting-sources', '',
        '--tools', '',
        '--disable-slash-commands',
        '--no-session-persistence',
        '--no-chrome',
        '--permission-mode', 'dontAsk',
        '--permission-prompts', 'none',
        'Rispondi soltanto OK.'
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $claudePath
    $startInfo.WorkingDirectory = $agentRoot
    $startInfo.Arguments = ($controlArguments | ForEach-Object { ConvertTo-QuotedProcessArgument $_ }) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'Avvio del controllo positivo hook non riuscito.'
    }
    $controlOutput = $process.StandardOutput.ReadToEnd()
    $controlError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $controlExitCode = $process.ExitCode
    $process.Dispose()
    ($controlOutput + $controlError) | Set-Content -Encoding utf8 $hookControlOutputPath

    if ($controlExitCode -ne 0) {
        throw "Il controllo positivo hook è terminato con codice $controlExitCode."
    }

    if (-not (Test-Path $hookControlSentinelPath)) {
        throw 'Il controllo positivo non ha osservato l esecuzione dell hook fidato.'
    }
    if (Test-Path $projectHookSentinelPath) {
        throw 'Il controllo positivo ha caricato un hook progetto che --restricted doveva ignorare.'
    }
}

New-Item -ItemType Directory -Path $serverRoot, $configurationRoot, $agentRoot, $externalRoot, $evidenceRoot, $projectClaudeRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $agentRoot 'workspace'), (Join-Path $agentRoot 'public-reference'), (Join-Path $agentRoot 'agent-tests') -Force | Out-Null
Set-Content -NoNewline -Encoding utf8 (Join-Path $agentRoot 'task-specification.md') $internalSentinel
Set-Content -NoNewline -Encoding utf8 (Join-Path $agentRoot 'public-reference\public.txt') 'PUBLIC_REFERENCE'
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
        benchmark_workspace = [ordered]@{
            command = $serverPath
            args = @($agentRoot)
            env = @{
                ENABLE_CLAUDEAI_MCP_SERVERS = 'false'
                MCP_WRITABLE_DIRECTORIES = 'workspace;agent-tests'
            }
        }
    }
}
Set-Utf8WithoutBom -LiteralPath $mcpConfigurationPath -Value ($mcpConfiguration | ConvertTo-Json -Depth 10)

@"
Set-Content -NoNewline -Encoding utf8 '$projectHookSentinelPath' 'PROJECT_HOOK_EXECUTED'
"@ | Set-Content -Encoding utf8 $projectHookPath
@"
Set-Content -NoNewline -Encoding utf8 '$trustedHookSentinelPath' 'TRUSTED_HOOK_EXECUTED'
"@ | Set-Content -Encoding utf8 $trustedHookPath

$projectSettings = [ordered]@{
    hooks = [ordered]@{
        SessionStart = @(
            [ordered]@{
                hooks = @(
                    [ordered]@{
                        type = 'command'
                        command = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$projectHookPath`""
                    }
                )
            }
        )
    }
}
$trustedSettings = [ordered]@{
    disableAllHooks = $true
    env = [ordered]@{
        CLAUDE_AGENT_SDK_DISABLE_BUILTIN_AGENTS = '1'
        ENABLE_CLAUDEAI_MCP_SERVERS = 'false'
    }
    hooks = [ordered]@{
        SessionStart = @(
            [ordered]@{
                hooks = @(
                    [ordered]@{
                        type = 'command'
                        command = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$trustedHookPath`""
                    }
                )
            }
        )
    }
}
$hookControlSettings = [ordered]@{
    disableAllHooks = $false
    env = [ordered]@{
        CLAUDE_AGENT_SDK_DISABLE_BUILTIN_AGENTS = '1'
    }
    hooks = [ordered]@{
        SessionStart = @(
            [ordered]@{
                hooks = @(
                    [ordered]@{
                        type = 'command'
                        command = "powershell -NoProfile -Command `"Set-Content -NoNewline -Encoding utf8 '$hookControlSentinelPath' 'HOOK_POSITIVE_CONTROL_EXECUTED'`""
                    }
                )
            }
        )
    }
}
Set-Utf8WithoutBom -LiteralPath $projectSettingsPath -Value ($projectSettings | ConvertTo-Json -Depth 10)
Set-Utf8WithoutBom -LiteralPath $settingsPath -Value ($trustedSettings | ConvertTo-Json -Depth 10)
Set-Utf8WithoutBom -LiteralPath $hookControlSettingsPath -Value ($hookControlSettings | ConvertTo-Json -Depth 10)

try {
    if ($Mode -in @('Direct', 'All')) {
        Invoke-DirectVerification
    }
    if ($Mode -in @('Claude', 'All')) {
        Invoke-HookPositiveControl
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
