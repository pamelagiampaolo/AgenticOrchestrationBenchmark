[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$serverProject = Join-Path $repositoryRoot 'src\AgentWorkspaceMcpServer\AgentWorkspaceMcpServer.csproj'
$serverDll = Join-Path $repositoryRoot 'src\AgentWorkspaceMcpServer\bin\Debug\net10.0\AgentWorkspaceMcpServer.dll'
$realRunner = Join-Path $repositoryRoot 'scripts\legacy-isolation\Invoke-NodeBaselineSandboxProbe.ps1'
$timeoutRunner = Join-Path $repositoryRoot 'experiments\legacy-isolation-probes\McpSandboxBridgeProbe\TimeoutRunnerFixture.ps1'
$missingReportRunner = Join-Path $repositoryRoot 'experiments\legacy-isolation-probes\McpSandboxBridgeProbe\MissingReportRunnerFixture.ps1'
$wsbPath = (Get-Command wsb.exe -ErrorAction Stop).Source
$probeRoot = Join-Path ([IO.Path]::GetTempPath()) ("mcp-sandbox-bridge-" + [guid]::NewGuid().ToString('N'))
$allowedRoot = Join-Path $probeRoot 'allowed-root'
$evidenceRoot = Join-Path $probeRoot 'host-evidence'
$resultPath = Join-Path $evidenceRoot 'mcp-sandbox-bridge-result.json'

function Get-RunningSandboxes {
    $result = & $wsbPath list --raw | ConvertFrom-Json
    return @($result.WindowsSandboxEnvironments)
}

function Start-McpServer {
    param(
        [string]$RunnerPath,
        [int]$TimeoutSeconds
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'dotnet.exe'
    $quotedArguments = @($serverDll, $allowedRoot, $RunnerPath) |
        ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
    $startInfo.Arguments = $quotedArguments -join ' '
    $startInfo.EnvironmentVariables['MCP_PUBLIC_SUITE_TIMEOUT_SECONDS'] = $TimeoutSeconds.ToString([Globalization.CultureInfo]::InvariantCulture)
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'Avvio del server MCP non riuscito.'
    }

    return $process
}

function Send-McpRequest {
    param(
        [Diagnostics.Process]$Process,
        [int]$Id,
        [string]$Method,
        [hashtable]$Parameters,
        [switch]$ObserveSandboxStart
    )

    $request = [ordered]@{ jsonrpc = '2.0'; id = $Id; method = $Method }
    if ($null -ne $Parameters) {
        $request.params = $Parameters
    }
    $Process.StandardInput.WriteLine(($request | ConvertTo-Json -Depth 10 -Compress))
    $Process.StandardInput.Flush()
    $responseTask = $Process.StandardOutput.ReadLineAsync()
    $observedSandboxIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $deadline = (Get-Date).AddMinutes(5)

    while (-not $responseTask.IsCompleted -and (Get-Date) -lt $deadline) {
        if ($ObserveSandboxStart) {
            foreach ($sandbox in Get-RunningSandboxes) {
                if (-not [string]::IsNullOrWhiteSpace([string]$sandbox.Id)) {
                    $observedSandboxIds.Add([string]$sandbox.Id) | Out-Null
                }
            }
        }
        Start-Sleep -Milliseconds 250
    }

    if (-not $responseTask.IsCompleted) {
        throw "Nessuna risposta MCP entro il limite per la richiesta $Id."
    }

    $line = $responseTask.GetAwaiter().GetResult()
    if ([string]::IsNullOrWhiteSpace($line)) {
        throw "Risposta MCP vuota per la richiesta $Id."
    }

    return [ordered]@{
        response = $line | ConvertFrom-Json
        observedSandboxIds = @($observedSandboxIds)
    }
}

function Stop-McpServer {
    param([Diagnostics.Process]$Process)

    try {
        $Process.StandardInput.Close()
        if (-not $Process.WaitForExit(10000)) {
            $Process.Kill($true)
            $Process.WaitForExit()
        }
        if ($Process.ExitCode -ne 0) {
            throw "Server MCP terminato con exit code $($Process.ExitCode): $($Process.StandardError.ReadToEnd())"
        }
    }
    finally {
        $Process.Dispose()
    }
}

New-Item -ItemType Directory -Path $allowedRoot, $evidenceRoot -Force | Out-Null
Set-Content -Encoding utf8 -LiteralPath (Join-Path $allowedRoot 'public.txt') -Value 'MCP_PUBLIC_FIXTURE'

& dotnet build $serverProject --no-restore | Out-Null
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $serverDll)) {
    throw 'Build del server MCP non riuscita.'
}

$initialSandboxCount = @(Get-RunningSandboxes).Count
if ($initialSandboxCount -ne 0) {
    throw 'La prova richiede zero Sandbox attive all’avvio.'
}

$realServer = Start-McpServer -RunnerPath $realRunner -TimeoutSeconds 300
try {
    $initialize = Send-McpRequest -Process $realServer -Id 1 -Method 'initialize' -Parameters @{
        protocolVersion = '2025-06-18'
        capabilities = @{}
        clientInfo = @{ name = 'mcp-sandbox-bridge-probe'; version = '0.1.0' }
    }
    $tools = Send-McpRequest -Process $realServer -Id 2 -Method 'tools/list' -Parameters @{}
    $toolNames = @($tools.response.result.tools | Select-Object -ExpandProperty name | Sort-Object)

    $countBeforeInvalid = @(Get-RunningSandboxes).Count
    $invalid = Send-McpRequest -Process $realServer -Id 3 -Method 'tools/call' -Parameters @{
        name = 'run_public_suite'
        arguments = @{ command = 'not-allowed' }
    }
    $countAfterInvalid = @(Get-RunningSandboxes).Count

    $valid = Send-McpRequest `
        -Process $realServer `
        -Id 4 `
        -Method 'tools/call' `
        -Parameters @{ name = 'run_public_suite'; arguments = @{} } `
        -ObserveSandboxStart
    $countAfterValid = @(Get-RunningSandboxes).Count
}
finally {
    Stop-McpServer -Process $realServer
}

$timeoutServer = Start-McpServer -RunnerPath $timeoutRunner -TimeoutSeconds 1
try {
    $timeout = Send-McpRequest -Process $timeoutServer -Id 5 -Method 'tools/call' -Parameters @{
        name = 'run_public_suite'
        arguments = @{}
    }
}
finally {
    Stop-McpServer -Process $timeoutServer
}
$countAfterTimeout = @(Get-RunningSandboxes).Count

$missingReportServer = Start-McpServer -RunnerPath $missingReportRunner -TimeoutSeconds 30
try {
    $missingReport = Send-McpRequest -Process $missingReportServer -Id 6 -Method 'tools/call' -Parameters @{
        name = 'run_public_suite'
        arguments = @{}
    }
}
finally {
    Stop-McpServer -Process $missingReportServer
}
$finalSandboxCount = @(Get-RunningSandboxes).Count

$structuredResult = $valid.response.result.structuredContent
$checks = [ordered]@{
    handshakeSucceeded = ($initialize.response.result.protocolVersion -eq '2025-06-18')
    exactToolInventory = (($toolNames -join ',') -eq 'list_files,read_file,run_public_suite')
    runToolHasClosedEmptySchema = @(
        $tools.response.result.tools |
            Where-Object name -eq 'run_public_suite' |
            Where-Object {
                $_.inputSchema.additionalProperties -eq $false -and
                ($_.inputSchema.properties | ConvertTo-Json -Compress) -eq '{}'
            }
        ).Count -eq 1
    invalidParametersRejected = ($invalid.response.error.code -eq -32602)
    invalidRejectedBeforeSandbox = ($countBeforeInvalid -eq 0 -and $countAfterInvalid -eq 0)
    newSandboxObserved = (@($valid.observedSandboxIds).Count -ge 1)
    validCallSucceeded = ($valid.response.result.isError -eq $false -and $null -eq $valid.response.error)
    buildPassed = ($structuredResult.BuildExitCode -eq 0)
    typecheckPassed = ($structuredResult.TypecheckExitCode -eq 0)
    publicSuitePassed = ($structuredResult.TestExitCode -eq 0)
    exactlyTwentySevenPassed = (
        $structuredResult.TotalTests -eq 27 -and
        $structuredResult.PassedTests -eq 27 -and
        $structuredResult.FailedTests -eq 0 -and
        $structuredResult.PendingTests -eq 0)
    validCallLeftNoSandbox = ($structuredResult.SandboxStopped -eq $true -and $countAfterValid -eq 0)
    timeoutReportedAsError = (
        $timeout.response.error.code -eq -32002 -and
        $timeout.response.error.message -like 'Timeout*')
    timeoutLeftNoSandbox = ($countAfterTimeout -eq 0)
    missingReportReportedAsError = (
        $missingReport.response.error.code -eq -32002 -and
        $missingReport.response.error.message -like '*mancante*')
    missingReportLeftNoSandbox = ($finalSandboxCount -eq 0)
}

[ordered]@{
    serverVersion = $initialize.response.result.serverInfo.version
    toolNames = $toolNames
    validResult = $structuredResult
    observedSandboxCountDuringValidCall = @($valid.observedSandboxIds).Count
    sandboxCounts = [ordered]@{
        initial = $initialSandboxCount
        beforeInvalid = $countBeforeInvalid
        afterInvalid = $countAfterInvalid
        afterValid = $countAfterValid
        afterTimeout = $countAfterTimeout
        final = $finalSandboxCount
    }
    errorResults = [ordered]@{
        invalidParameters = [ordered]@{ code = $invalid.response.error.code; message = $invalid.response.error.message }
        timeout = [ordered]@{ code = $timeout.response.error.code; message = $timeout.response.error.message }
        missingReport = [ordered]@{ code = $missingReport.response.error.code; message = $missingReport.response.error.message }
    }
    checks = $checks
    limits = @(
        'Only the valid MCP call launches the real P2.2 Windows Sandbox runner.',
        'Timeout and missing-report handling use controlled runner fixtures and do not re-test Windows Sandbox isolation.',
        'Claude, reserved tests, evaluator and definitive baseline designation are outside this probe.'
    )
} | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 -LiteralPath $resultPath

if (@($checks.Values | Where-Object { -not $_ }).Count -gt 0) {
    throw "Una o più verifiche MCP-Sandbox non sono state superate. Vedere $resultPath"
}

Write-Output "PROBE_ROOT=$probeRoot"
Write-Output "HOST_EVIDENCE=$evidenceRoot"
