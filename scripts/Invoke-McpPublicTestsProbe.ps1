[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$serverProject = Join-Path $repositoryRoot 'McpReadOnlyPrototype\McpReadOnlyPrototype.csproj'
$serverDll = Join-Path $repositoryRoot 'McpReadOnlyPrototype\bin\Debug\net10.0\McpReadOnlyPrototype.dll'
$benchmarkRoot = Join-Path $repositoryRoot 'BenchmarkTarget'
$nodePath = (Get-Command node.exe -ErrorAction Stop).Source
$vitestPath = Join-Path $benchmarkRoot 'node_modules\vitest\vitest.mjs'
$typeScriptPath = Join-Path $benchmarkRoot 'node_modules\typescript\lib\tsc.js'
$probeRoot = Join-Path $repositoryRoot ('TestResults\mcp-public-tests-' + [guid]::NewGuid().ToString('N'))
$agentRoot = Join-Path $probeRoot 'agent-root'
$workspace = Join-Path $agentRoot 'workspace'
$agentTests = Join-Path $agentRoot 'agent-tests'
$publicReference = Join-Path $agentRoot 'public-reference\tests\public'
$workerRoot = Join-Path $probeRoot 'worker'
$resultsRoot = Join-Path $probeRoot 'results'
$auditPath = Join-Path $resultsRoot 'command-audit.jsonl'
$agentTestPath = Join-Path $agentTests 'tests\agent\probe.test.ts'

try {
    & dotnet build $serverProject --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Build del server MCP fallita con exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $vitestPath -PathType Leaf)) {
        throw "Entry point Vitest non trovato: $vitestPath"
    }

    New-Item -ItemType Directory -Path $workspace, $agentTests, $publicReference, $workerRoot, $resultsRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $benchmarkRoot 'src') -Destination $workspace -Recurse
    Copy-Item -LiteralPath (Join-Path $benchmarkRoot 'tsconfig.json') -Destination $workspace
    Copy-Item -LiteralPath (Join-Path $benchmarkRoot 'package.json') -Destination $workspace
    Copy-Item -LiteralPath (Join-Path $benchmarkRoot 'tests\public\inventory-management.test.ts') -Destination $publicReference
    Copy-Item -LiteralPath (Join-Path $benchmarkRoot 'tests\public\order-management.test.ts') -Destination $publicReference
    New-Item -ItemType Directory -Path (Split-Path -Parent $agentTestPath) -Force | Out-Null
    Set-Content -LiteralPath $agentTestPath -Encoding utf8 -Value "import { describe, expect, it } from 'vitest'; describe('agent probe', () => { it('runs separately', () => expect(2 + 2).toBe(4)); });"

    $environment = [ordered]@{
        MCP_WRITABLE_DIRECTORIES = 'workspace;agent-tests'
        MCP_PUBLIC_TEST_NODE = $nodePath
        MCP_PUBLIC_TEST_VITEST = $vitestPath
        MCP_PUBLIC_TEST_WORKER_ROOT = $workerRoot
        MCP_COMMAND_AUDIT_PATH = $auditPath
        MCP_PUBLIC_TEST_TIMEOUT_SECONDS = '30'
        MCP_TYPECHECK_NODE = $nodePath
        MCP_TYPECHECK_TSC = $typeScriptPath
        MCP_TYPECHECK_WORKER_ROOT = $workerRoot
        MCP_TYPECHECK_TIMEOUT_SECONDS = '30'
        MCP_BUILD_NODE = $nodePath
        MCP_BUILD_TSC = $typeScriptPath
        MCP_BUILD_WORKER_ROOT = $workerRoot
        MCP_BUILD_TIMEOUT_SECONDS = '30'
        MCP_AGENT_TEST_NODE = $nodePath
        MCP_AGENT_TEST_VITEST = $vitestPath
        MCP_AGENT_TEST_WORKER_ROOT = $workerRoot
        MCP_AGENT_TEST_TIMEOUT_SECONDS = '30'
    }
    $previousEnvironment = @{}
    foreach ($entry in $environment.GetEnumerator()) {
        $previousEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    try {
        $requests = @(
            ([ordered]@{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{} } | ConvertTo-Json -Depth 10 -Compress)
            ([ordered]@{ jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{} } | ConvertTo-Json -Depth 10 -Compress)
            ([ordered]@{ jsonrpc = '2.0'; id = 3; method = 'tools/call'; params = @{ name = 'run_public_tests'; arguments = @{ command = 'arbitrary' } } } | ConvertTo-Json -Depth 10 -Compress)
            ([ordered]@{ jsonrpc = '2.0'; id = 4; method = 'tools/call'; params = @{ name = 'run_public_tests'; arguments = @{} } } | ConvertTo-Json -Depth 10 -Compress)
            ([ordered]@{ jsonrpc = '2.0'; id = 5; method = 'tools/call'; params = @{ name = 'run_typecheck'; arguments = @{ command = 'arbitrary' } } } | ConvertTo-Json -Depth 10 -Compress)
            ([ordered]@{ jsonrpc = '2.0'; id = 6; method = 'tools/call'; params = @{ name = 'run_typecheck'; arguments = @{} } } | ConvertTo-Json -Depth 10 -Compress)
            ([ordered]@{ jsonrpc = '2.0'; id = 7; method = 'tools/call'; params = @{ name = 'run_build'; arguments = @{ command = 'arbitrary' } } } | ConvertTo-Json -Depth 10 -Compress)
            ([ordered]@{ jsonrpc = '2.0'; id = 8; method = 'tools/call'; params = @{ name = 'run_build'; arguments = @{} } } | ConvertTo-Json -Depth 10 -Compress)
            ([ordered]@{ jsonrpc = '2.0'; id = 9; method = 'tools/call'; params = @{ name = 'run_agent_tests'; arguments = @{ command = 'arbitrary' } } } | ConvertTo-Json -Depth 10 -Compress)
            ([ordered]@{ jsonrpc = '2.0'; id = 10; method = 'tools/call'; params = @{ name = 'run_agent_tests'; arguments = @{} } } | ConvertTo-Json -Depth 10 -Compress)
        )
        $stderrPath = Join-Path $resultsRoot 'server-stderr.txt'
        $previousErrorPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $responseLines = @($requests | & dotnet $serverDll $agentRoot 2> $stderrPath)
        $serverExitCode = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorPreference
    }
    finally {
        foreach ($entry in $previousEnvironment.GetEnumerator()) {
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        }
    }

    if ($serverExitCode -ne 0 -or $responseLines.Count -ne 10) {
        $diagnostic = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { '' }
        throw "Server MCP terminato con exit code $serverExitCode e $($responseLines.Count) risposte. $diagnostic"
    }

    $initialize, $toolsResponse, $invalidResponse, $runResponse, $invalidTypecheckResponse, $typecheckResponse, $invalidBuildResponse, $buildResponse, $invalidAgentTestResponse, $agentTestResponse = @($responseLines | ForEach-Object { $_ | ConvertFrom-Json })
    $standardError = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { '' }
    if ($null -eq $runResponse.result -or $null -eq $runResponse.result.structuredContent) {
        throw "Risposta MCP di esecuzione inattesa: $($responseLines -join ' | '). Stderr: $standardError"
    }

    $toolNames = @($toolsResponse.result.tools | ForEach-Object name)
    $expectedTools = @('list_files', 'read_file', 'write_file', 'delete_file', 'move_file', 'run_public_tests', 'run_typecheck', 'run_build', 'run_agent_tests')
    $auditLines = @(Get-Content -LiteralPath $auditPath)
    $remainingWorkerEntries = @(Get-ChildItem -LiteralPath $workerRoot -Force)
    $counts = $runResponse.result.structuredContent.Tests
    $normalizedStandardError = if ([string]::IsNullOrEmpty($standardError)) { '' } else { $standardError.Trim() }
    $summary = [ordered]@{
        protocolVersion = $initialize.result.protocolVersion
        tools = $toolNames
        exactToolInventory = (($toolNames -join ',') -eq ($expectedTools -join ','))
        arbitraryParameterRejected = ($invalidResponse.error.code -eq -32602)
        status = $runResponse.result.structuredContent.Status
        exitCode = $runResponse.result.structuredContent.ExitCode
        totalTests = $counts.Total
        passedTests = $counts.Passed
        failedTests = $counts.Failed
        pendingTests = $counts.Pending
        typecheckArbitraryParameterRejected = ($invalidTypecheckResponse.error.code -eq -32602)
        typecheckStatus = $typecheckResponse.result.structuredContent.Status
        typecheckExitCode = $typecheckResponse.result.structuredContent.ExitCode
        buildArbitraryParameterRejected = ($invalidBuildResponse.error.code -eq -32602)
        buildStatus = $buildResponse.result.structuredContent.Status
        buildExitCode = $buildResponse.result.structuredContent.ExitCode
        agentTestArbitraryParameterRejected = ($invalidAgentTestResponse.error.code -eq -32602)
        agentTestOperation = $agentTestResponse.result.structuredContent.Operation
        agentTestStatus = $agentTestResponse.result.structuredContent.Status
        agentTestExitCode = $agentTestResponse.result.structuredContent.ExitCode
        agentTestTotalTests = $agentTestResponse.result.structuredContent.Tests.Total
        agentTestPassedTests = $agentTestResponse.result.structuredContent.Tests.Passed
        auditRecordCount = $auditLines.Count
        workerResidualEntryCount = $remainingWorkerEntries.Count
        serverStandardError = $normalizedStandardError
    }

    $failures = [Collections.Generic.List[string]]::new()
    if (-not $summary.exactToolInventory) { $failures.Add('inventario strumenti inatteso') }
    if (-not $summary.arbitraryParameterRejected) { $failures.Add('parametro arbitrario non rifiutato') }
    if (-not $summary.typecheckArbitraryParameterRejected) { $failures.Add('parametro typecheck arbitrario non rifiutato') }
    if (-not $summary.buildArbitraryParameterRejected) { $failures.Add('parametro build arbitrario non rifiutato') }
    if (-not $summary.agentTestArbitraryParameterRejected) { $failures.Add('parametro agent test arbitrario non rifiutato') }
    if ($summary.status -ne 'completed' -or $summary.exitCode -ne 0) { $failures.Add('esecuzione pubblica non completata') }
    if ($summary.totalTests -ne 27 -or $summary.passedTests -ne 27 -or $summary.failedTests -ne 0 -or $summary.pendingTests -ne 0) {
        $failures.Add('conteggi pubblici diversi da 27/27')
    }
    if ($summary.typecheckStatus -ne 'completed' -or $summary.typecheckExitCode -ne 0) { $failures.Add('typecheck non completato') }
    if ($summary.buildStatus -ne 'completed' -or $summary.buildExitCode -ne 0) { $failures.Add('build non completata') }
    if ($summary.agentTestOperation -ne 'agent-tests' -or $summary.agentTestStatus -ne 'completed' -or $summary.agentTestExitCode -ne 0) { $failures.Add('test agente non completati separatamente') }
    if ($summary.agentTestTotalTests -ne 1 -or $summary.agentTestPassedTests -ne 1) { $failures.Add('conteggi test agente diversi da 1/1') }
    if ($summary.auditRecordCount -ne 4) { $failures.Add('audit diverso da quattro invocazioni') }
    if ($summary.workerResidualEntryCount -ne 0) { $failures.Add('residui nella worker root') }
    if (-not [string]::IsNullOrEmpty($summary.serverStandardError)) { $failures.Add('stderr del server non vuoto') }

    $summary | ConvertTo-Json -Depth 10
    if ($failures.Count -gt 0) {
        throw ('Probe MCP fallita: ' + ($failures -join '; '))
    }
}
finally {
    if (Test-Path -LiteralPath $probeRoot) {
        Remove-Item -LiteralPath $probeRoot -Recurse -Force
    }
}
