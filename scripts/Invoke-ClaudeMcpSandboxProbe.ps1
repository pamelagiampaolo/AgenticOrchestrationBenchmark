[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'McpReadOnlyPrototype\McpReadOnlyPrototype.csproj'
$runnerPath = Join-Path $repositoryRoot 'scripts\Invoke-NodeBaselineSandboxProbe.ps1'
$claudePath = (Get-Command claude -ErrorAction Stop).Source
$wsbPath = (Get-Command wsb.exe -ErrorAction Stop).Source
$runRoot = Join-Path ([IO.Path]::GetTempPath()) ("claude-mcp-sandbox-" + [guid]::NewGuid().ToString('N'))
$trustedRoot = Join-Path $runRoot 'trusted'
$serverRoot = Join-Path $trustedRoot 'server'
$configurationRoot = Join-Path $trustedRoot 'configuration'
$agentRoot = Join-Path $runRoot 'agent-workspace'
$evidenceRoot = Join-Path $runRoot 'host-evidence'
$serverPath = Join-Path $serverRoot 'McpReadOnlyPrototype.exe'
$mcpConfigurationPath = Join-Path $configurationRoot 'mcp.json'
$settingsPath = Join-Path $configurationRoot 'settings.json'
$nugetConfigurationPath = Join-Path $configurationRoot 'NuGet.Config'
$streamPath = Join-Path $evidenceRoot 'claude-stream.jsonl'
$stderrPath = Join-Path $evidenceRoot 'claude-stderr.txt'
$manifestPath = Join-Path $evidenceRoot 'run-manifest.json'
$checksPath = Join-Path $evidenceRoot 'host-checks.json'
$prompt = 'Invoca esattamente una volta run_public_suite con un oggetto argomenti vuoto. Non invocare list_files, read_file o altri strumenti. Dopo il risultato, rispondi soltanto completato.'

function ConvertTo-QuotedProcessArgument {
    param([string]$Value)

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Get-RunningSandboxes {
    $result = & $wsbPath list --raw | ConvertFrom-Json
    return @($result.WindowsSandboxEnvironments)
}

New-Item -ItemType Directory -Path $serverRoot, $configurationRoot, $agentRoot, $evidenceRoot -Force | Out-Null
Set-Content -NoNewline -Encoding utf8 -LiteralPath (Join-Path $agentRoot 'public.txt') -Value 'PUBLIC_P3_SESSION'

@'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
</configuration>
'@ | Set-Content -Encoding utf8 -LiteralPath $nugetConfigurationPath

& dotnet restore $projectPath --configfile $nugetConfigurationPath --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore offline non riuscito con codice $LASTEXITCODE."
}
& dotnet publish $projectPath --configuration Release --output $serverRoot --no-restore --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish non riuscito con codice $LASTEXITCODE."
}

$mcpConfiguration = [ordered]@{
    mcpServers = [ordered]@{
        readonly = [ordered]@{
            type = 'stdio'
            command = $serverPath
            args = @($agentRoot, $runnerPath)
            env = @{ MCP_PUBLIC_SUITE_TIMEOUT_SECONDS = '300' }
        }
    }
}
$mcpConfiguration | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 -LiteralPath $mcpConfigurationPath

$trustedSettings = [ordered]@{
    disableAllHooks = $true
    env = [ordered]@{ CLAUDE_AGENT_SDK_DISABLE_BUILTIN_AGENTS = '1' }
}
$trustedSettings | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 -LiteralPath $settingsPath

$arguments = @(
    '-p',
    '--output-format', 'stream-json',
    '--verbose',
    '--include-hook-events',
    '--restricted',
    '--strict-mcp-config',
    '--mcp-config', $mcpConfigurationPath,
    '--settings', $settingsPath,
    '--setting-sources', '',
    '--tools', '',
    '--allowedTools', 'mcp__readonly__list_files,mcp__readonly__read_file,mcp__readonly__run_public_suite',
    '--disallowedTools', 'Bash,PowerShell,Read,Write,Edit,Glob,Grep,WebFetch,WebSearch,Task,Agent,Skill',
    '--disable-slash-commands',
    '--no-session-persistence',
    '--no-chrome',
    '--permission-mode', 'dontAsk',
    '--permission-prompts', 'none',
    $prompt
)

$initialSandboxes = @(Get-RunningSandboxes)
if ($initialSandboxes.Count -ne 0) {
    throw 'La prova richiede zero Sandbox attive all’avvio.'
}
$existingBaselineRuns = @(
    Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'node-baseline-sandbox-*' |
        Select-Object -ExpandProperty FullName
)

$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $claudePath
$startInfo.WorkingDirectory = $agentRoot
$startInfo.Arguments = ($arguments | ForEach-Object { ConvertTo-QuotedProcessArgument $_ }) -join ' '
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.CreateNoWindow = $true
$process = [Diagnostics.Process]::new()
$process.StartInfo = $startInfo
if (-not $process.Start()) {
    throw 'Avvio di Claude CLI non riuscito.'
}

$stdoutTask = $process.StandardOutput.ReadToEndAsync()
$stderrTask = $process.StandardError.ReadToEndAsync()
$observedSandboxIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$deadline = (Get-Date).AddMinutes(8)
while (-not $process.HasExited -and (Get-Date) -lt $deadline) {
    foreach ($sandbox in Get-RunningSandboxes) {
        if (-not [string]::IsNullOrWhiteSpace([string]$sandbox.Id)) {
            $observedSandboxIds.Add([string]$sandbox.Id) | Out-Null
        }
    }
    Start-Sleep -Milliseconds 250
}
if (-not $process.HasExited) {
    $process.Kill($true)
    $process.WaitForExit()
    throw 'Claude CLI non è terminata entro otto minuti.'
}

$exitCode = $process.ExitCode
$standardOutput = $stdoutTask.GetAwaiter().GetResult()
$standardError = $stderrTask.GetAwaiter().GetResult()
$process.Dispose()
$standardOutput | Set-Content -Encoding utf8 -LiteralPath $streamPath
$standardError | Set-Content -Encoding utf8 -LiteralPath $stderrPath

$events = @(
    $standardOutput -split "`r?`n" |
        Where-Object { $_.Trim() } |
        ForEach-Object { $_ | ConvertFrom-Json }
)
$initialization = $events |
    Where-Object { $_.type -eq 'system' -and $_.subtype -eq 'init' } |
    Select-Object -First 1
$toolUses = @(
    $events |
        Where-Object { $_.type -eq 'assistant' } |
        ForEach-Object { $_.message.content } |
        Where-Object { $_.type -eq 'tool_use' }
)
$toolResults = @(
    $events |
        Where-Object { $_.type -eq 'user' } |
        ForEach-Object { $_.message.content } |
        Where-Object { $_.type -eq 'tool_result' }
)
$hookEvents = @($events | Where-Object { $_.type -eq 'system' -and $_.subtype -match 'hook' })
$finalResult = $events | Where-Object { $_.type -eq 'result' } | Select-Object -Last 1
$expectedTools = @(
    'mcp__readonly__list_files',
    'mcp__readonly__read_file',
    'mcp__readonly__run_public_suite'
)

$newBaselineRuns = @(
    Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'node-baseline-sandbox-*' |
        Where-Object { $_.FullName -notin $existingBaselineRuns }
)
$authoritativeReports = @(
    $newBaselineRuns |
        ForEach-Object { Join-Path $_.FullName 'host-evidence\host-verification.json' } |
        Where-Object { Test-Path -LiteralPath $_ }
)
$authoritativeReport = if ($authoritativeReports.Count -eq 1) {
    Get-Content -Raw -LiteralPath $authoritativeReports[0] | ConvertFrom-Json
} else {
    $null
}

$toolUseInputPropertyCount = if ($toolUses.Count -eq 1) {
    @($toolUses[0].input.PSObject.Properties).Count
} else {
    -1
}
$toolResultPayload = $null
if ($toolResults.Count -eq 1 -and $toolResults[0].content -is [string]) {
    try {
        $toolResultPayload = $toolResults[0].content | ConvertFrom-Json
    }
    catch {
        $toolResultPayload = $null
    }
}
$finalSandboxCount = @(Get-RunningSandboxes).Count
$checks = [ordered]@{
    cliExitCodeZero = ($exitCode -eq 0)
    initializationObserved = ($null -ne $initialization)
    exactToolInventory = ((@($initialization.tools | Sort-Object) -join ',') -eq (@($expectedTools | Sort-Object) -join ','))
    singleMcpServerConnected = (
        @($initialization.mcp_servers).Count -eq 1 -and
        $initialization.mcp_servers[0].name -eq 'readonly' -and
        $initialization.mcp_servers[0].status -eq 'connected')
    noSlashCommands = (@($initialization.slash_commands).Count -eq 0)
    noSkills = (@($initialization.skills).Count -eq 0)
    builtInSubagentsRemoved = (@($initialization.agents | Where-Object { $_ -ne 'claude' }).Count -eq 0)
    noHookEventsObserved = ($hookEvents.Count -eq 0)
    exactlyOneToolCall = ($toolUses.Count -eq 1)
    onlyRunPublicSuiteCalled = ($toolUses.Count -eq 1 -and $toolUses[0].name -eq 'mcp__readonly__run_public_suite')
    toolArgumentsEmpty = ($toolUseInputPropertyCount -eq 0)
    exactlyOneToolResult = ($toolResults.Count -eq 1)
    toolResultSuccessful = ($toolResults.Count -eq 1 -and -not [bool]$toolResults[0].is_error)
    toolResultContainsTwentySevenPassed = (
        $null -ne $toolResultPayload -and
        $toolResultPayload.PassedTests -eq 27 -and
        $toolResultPayload.TotalTests -eq 27 -and
        $toolResultPayload.FailedTests -eq 0 -and
        $toolResultPayload.PendingTests -eq 0 -and
        $toolResultPayload.IsSuccessful -eq $true)
    newSandboxObserved = ($observedSandboxIds.Count -ge 1)
    oneAuthoritativeReportObserved = ($authoritativeReports.Count -eq 1)
    authoritativeReportPassed = (
        $null -ne $authoritativeReport -and
        $authoritativeReport.guestResult.passedTests -eq 27 -and
        $authoritativeReport.guestResult.totalTests -eq 27 -and
        $authoritativeReport.checks.sandboxStopped -eq $true)
    noResidualSandbox = ($finalSandboxCount -eq 0)
    noDelegationObserved = ($finalResult.subagent_stats.spawned -eq 0)
}

$redactedArguments = @(
    $arguments |
        ForEach-Object { if ($_ -eq $prompt) { '<PROMPT_RECORDED_SEPARATELY>' } else { $_ } }
)
[ordered]@{
    timestampUtc = [DateTimeOffset]::UtcNow
    claudeVersion = (& $claudePath --version | Out-String).Trim()
    arguments = $redactedArguments
    prompt = $prompt
    exitCode = $exitCode
    configurationSourcesRequested = @()
    mcpServerNames = @($initialization.mcp_servers | Select-Object -ExpandProperty name)
    toolInventory = @($initialization.tools)
    declaredAgents = @($initialization.agents)
    declaredPlugins = @($initialization.plugins)
    serverOutsideAgentWorkspace = -not $serverPath.StartsWith($agentRoot, [StringComparison]::OrdinalIgnoreCase)
    configurationOutsideAgentWorkspace = -not $mcpConfigurationPath.StartsWith($agentRoot, [StringComparison]::OrdinalIgnoreCase)
    configurationSourcesEffective = 'Non esposte da system/init; non dimostrate oltre ai controlli P1.1.'
} | ConvertTo-Json -Depth 15 | Set-Content -Encoding utf8 -LiteralPath $manifestPath

[ordered]@{
    observedSandboxCount = $observedSandboxIds.Count
    finalSandboxCount = $finalSandboxCount
    toolCallNames = @($toolUses.name)
    toolUseInputPropertyCount = $toolUseInputPropertyCount
    toolResultIsError = if ($toolResults.Count -eq 1) { [bool]$toolResults[0].is_error } else { $null }
    toolResultPublicResult = if ($null -ne $toolResultPayload) {
        [ordered]@{
            totalTests = $toolResultPayload.TotalTests
            passedTests = $toolResultPayload.PassedTests
            failedTests = $toolResultPayload.FailedTests
            pendingTests = $toolResultPayload.PendingTests
            isSuccessful = $toolResultPayload.IsSuccessful
        }
    } else { $null }
    authoritativeReportCount = $authoritativeReports.Count
    authoritativePublicResult = if ($null -ne $authoritativeReport) {
        [ordered]@{
            totalTests = $authoritativeReport.guestResult.totalTests
            passedTests = $authoritativeReport.guestResult.passedTests
            failedTests = $authoritativeReport.guestResult.failedTests
            pendingTests = $authoritativeReport.guestResult.pendingTests
            sandboxStopped = $authoritativeReport.checks.sandboxStopped
        }
    } else { $null }
    checks = $checks
} | ConvertTo-Json -Depth 15 | Set-Content -Encoding utf8 -LiteralPath $checksPath

if ($exitCode -ne 0) {
    throw "Claude CLI è terminata con codice $exitCode. Vedere $stderrPath"
}
if (@($checks.Values | Where-Object { -not $_ }).Count -gt 0) {
    throw "Una o più verifiche Claude-MCP-Sandbox non sono state superate. Vedere $checksPath"
}

Write-Output "RUN_ROOT=$runRoot"
Write-Output "HOST_EVIDENCE=$evidenceRoot"
