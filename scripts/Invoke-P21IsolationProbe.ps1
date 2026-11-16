[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$probeSourceRoot = Join-Path $repositoryRoot 'experiments\legacy-isolation-probes\P21IsolationProbe'
$tcpServerScript = Join-Path $PSScriptRoot 'P21TcpSentinelServer.ps1'
$wsbPath = 'C:\Users\Utente\AppData\Local\Microsoft\WindowsApps\wsb.exe'
$runRoot = Join-Path ([IO.Path]::GetTempPath()) ("p21-isolation-" + [guid]::NewGuid().ToString('N'))
$inputRoot = Join-Path $runRoot 'authorized-input'
$projectInputRoot = Join-Path $inputRoot 'project'
$mainOutputRoot = Join-Path $runRoot 'sandbox-output-main'
$timeoutOutputRoot = Join-Path $runRoot 'sandbox-output-timeout'
$persistenceOutputRoot = Join-Path $runRoot 'sandbox-output-persistence'
$hostEvidenceRoot = Join-Path $runRoot 'host-evidence'
$trustedConfigRoot = Join-Path $runRoot 'trusted-config'
$reservedRoot = Join-Path $runRoot 'simulated-reserved'
$trustedResultsRoot = Join-Path $runRoot 'trusted-results'
$previousOutputRoot = Join-Path $runRoot 'previous-run-output'
$credentialRoot = Join-Path $runRoot 'simulated-credentials'
$endpointLogPath = Join-Path $hostEvidenceRoot 'tcp-endpoint-connections.log'
$endpointReadyPath = Join-Path $hostEvidenceRoot 'tcp-endpoint-ready.txt'
$hostReportPath = Join-Path $hostEvidenceRoot 'host-verification.json'
$manifestPath = Join-Path $hostEvidenceRoot 'artifact-manifest.json'
$hostOnlyExecutionMarkerPath = Join-Path $trustedConfigRoot 'host-only-executed.txt'
$activeSandboxIds = [Collections.Generic.List[string]]::new()
$endpointProcess = $null

function ConvertTo-QuotedProcessArgument {
    param([string]$Value)

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Get-StringSha256 {
    param([string]$Value)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value)))).Replace('-', '')
    }
    finally {
        $algorithm.Dispose()
    }
}

function Start-EmptySandbox {
    param([string]$OutputRoot)

    $escapedInput = [Security.SecurityElement]::Escape($inputRoot)
    $escapedOutput = [Security.SecurityElement]::Escape($OutputRoot)
    $configuration = @"
<Configuration>
  <VGpu>Disable</VGpu>
  <Networking>Disable</Networking>
  <AudioInput>Disable</AudioInput>
  <VideoInput>Disable</VideoInput>
  <ProtectedClient>Enable</ProtectedClient>
  <PrinterRedirection>Disable</PrinterRedirection>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$escapedInput</HostFolder>
      <SandboxFolder>C:\P21Input</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$escapedOutput</HostFolder>
      <SandboxFolder>C:\P21Output</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
</Configuration>
"@

    $startText = (& $wsbPath start --config $configuration --raw 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "wsb start non riuscito: $startText"
    }

    $startResult = $startText | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($startResult.Id)) {
        throw 'wsb start non ha restituito un ID.'
    }

    $activeSandboxIds.Add([string]$startResult.Id)
    return [ordered]@{ id = [string]$startResult.Id; configuration = $configuration }
}

function Stop-Sandbox {
    param([string]$Id)

    & $wsbPath stop --id $Id --raw | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "wsb stop non riuscito per $Id."
    }

    $activeSandboxIds.Remove($Id) | Out-Null
}

function Start-WsbExecProcess {
    param(
        [string]$Id,
        [string]$Command
    )

    $arguments = @('exec', '--id', $Id, '--command', $Command, '--run-as', 'System', '--raw')
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $wsbPath
    $startInfo.Arguments = ($arguments | ForEach-Object { ConvertTo-QuotedProcessArgument $_ }) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'Impossibile avviare wsb exec.'
    }

    return $process
}

function Invoke-WsbExec {
    param(
        [string]$Id,
        [string]$Command,
        [int]$TimeoutSeconds
    )

    $process = Start-WsbExecProcess -Id $Id -Command $Command
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            throw "wsb exec non è terminato entro $TimeoutSeconds secondi."
        }

        return [ordered]@{
            exitCode = $process.ExitCode
            standardOutput = $process.StandardOutput.ReadToEnd()
            standardError = $process.StandardError.ReadToEnd()
        }
    }
    finally {
        $process.Dispose()
    }
}

function Wait-ForFile {
    param(
        [string]$Path,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            return $true
        }
        Start-Sleep -Milliseconds 250
    }

    return $false
}

function Get-DefaultNetworkInformation {
    $route = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction Stop |
        Sort-Object RouteMetric, InterfaceMetric |
        Select-Object -First 1
    $address = Get-NetIPAddress -InterfaceIndex $route.InterfaceIndex -AddressFamily IPv4 -ErrorAction Stop |
        Where-Object { $_.IPAddress -notlike '169.254.*' } |
        Select-Object -First 1
    if ($null -eq $route -or $null -eq $address) {
        throw 'Impossibile determinare indirizzo host e gateway per la prova di rete.'
    }

    return [ordered]@{ hostAddress = $address.IPAddress; gatewayAddress = $route.NextHop }
}

function Test-AllAttemptResults {
    param($Report)

    $protectedReadsDenied = @($Report.attempts.protectedReads.PSObject.Properties | Where-Object { -not $_.Value.denied }).Count -eq 0
    $protectedListingsDenied = @($Report.attempts.protectedDirectoryListings.PSObject.Properties | Where-Object { -not $_.Value.denied }).Count -eq 0
    return [ordered]@{
        internalReadSucceeded = [bool]$Report.attempts.internalRead.succeeded
        scratchLifecycleSucceeded = [bool]$Report.attempts.scratchLifecycle.succeeded
        outputWriteSucceeded = [bool]$Report.attempts.outputWrite.succeeded
        inputWriteDenied = [bool]$Report.attempts.inputWrite.denied
        protectedReadsDenied = $protectedReadsDenied
        protectedListingsDenied = $protectedListingsDenied
        traversalDenied = [bool]$Report.attempts.traversalRead.denied
        uncDenied = [bool]$Report.attempts.uncRead.denied
        junctionCreated = [bool]$Report.attempts.junctionEscape.junctionCreated
        junctionReadDenied = [bool]$Report.attempts.junctionEscape.readDenied
        hostProcessLaunchDenied = [bool]$Report.attempts.hostProcessLaunch.denied
        exactEnvironmentAllowlist = [bool]$Report.attempts.sensitiveEnvironment.exactAllowlist
        publicDnsDenied = [bool]$Report.attempts.publicDns.denied
        publicTcpDenied = [bool]$Report.attempts.publicTcp.denied
        hostEndpointTcpDenied = [bool]$Report.attempts.hostEndpointTcp.denied
        lanTcpDenied = [bool]$Report.attempts.lanTcp.denied
        noUpNonLoopbackAdapters = [bool]$Report.attempts.networkAdapters.noUpNonLoopbackAdapters
    }
}

New-Item -ItemType Directory -Path @(
    $inputRoot,
    $projectInputRoot,
    $mainOutputRoot,
    $timeoutOutputRoot,
    $persistenceOutputRoot,
    $hostEvidenceRoot,
    $trustedConfigRoot,
    $reservedRoot,
    $trustedResultsRoot,
    $previousOutputRoot,
    $credentialRoot
) -Force | Out-Null

$internalSentinel = 'P21_INTERNAL_' + [guid]::NewGuid().ToString('N')
$configSentinel = 'P21_CONFIG_' + [guid]::NewGuid().ToString('N')
$reservedSentinel = 'P21_RESERVED_' + [guid]::NewGuid().ToString('N')
$resultSentinel = 'P21_RESULT_' + [guid]::NewGuid().ToString('N')
$previousOutputSentinel = 'P21_PREVIOUS_' + [guid]::NewGuid().ToString('N')
$credentialSentinel = 'P21_CREDENTIAL_' + [guid]::NewGuid().ToString('N')
$protectedSentinels = @($configSentinel, $reservedSentinel, $resultSentinel, $previousOutputSentinel, $credentialSentinel)

$internalPath = Join-Path $inputRoot 'internal.txt'
$configSentinelPath = Join-Path $trustedConfigRoot 'config-sentinel.txt'
$reservedSentinelPath = Join-Path $reservedRoot 'reserved-sentinel.txt'
$resultSentinelPath = Join-Path $trustedResultsRoot 'previous-result-sentinel.txt'
$previousOutputSentinelPath = Join-Path $previousOutputRoot 'previous-output-sentinel.txt'
$credentialSentinelPath = Join-Path $credentialRoot 'credential-sentinel.txt'
$hostOnlyExecutablePath = Join-Path $trustedConfigRoot 'host-only-probe.cmd'

Set-Content -NoNewline -Encoding utf8 $internalPath $internalSentinel
Set-Content -NoNewline -Encoding utf8 $configSentinelPath $configSentinel
Set-Content -NoNewline -Encoding utf8 $reservedSentinelPath $reservedSentinel
Set-Content -NoNewline -Encoding utf8 $resultSentinelPath $resultSentinel
Set-Content -NoNewline -Encoding utf8 $previousOutputSentinelPath $previousOutputSentinel
Set-Content -NoNewline -Encoding utf8 $credentialSentinelPath $credentialSentinel
@"
@echo off
echo HOST_ONLY_EXECUTED>"$hostOnlyExecutionMarkerPath"
"@ | Set-Content -Encoding ascii $hostOnlyExecutablePath

Copy-Item -Path (Join-Path $probeSourceRoot '*') -Destination $projectInputRoot -Recurse -Force

$network = Get-DefaultNetworkInformation
$portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$portProbe.Start()
$endpointPort = ([Net.IPEndPoint]$portProbe.LocalEndpoint).Port
$portProbe.Stop()

$hostName = [Environment]::MachineName
$probeConfig = [ordered]@{
    inputRoot = 'C:\P21Input'
    outputRoot = 'C:\P21Output'
    workRoot = 'C:\P21Work'
    internalExpectedValue = $internalSentinel
    protectedFiles = [ordered]@{
        trustedConfig = $configSentinelPath
        simulatedReserved = $reservedSentinelPath
        trustedResults = $resultSentinelPath
        previousRunOutput = $previousOutputSentinelPath
        simulatedCredential = $credentialSentinelPath
    }
    protectedDirectories = [ordered]@{
        trustedConfig = $trustedConfigRoot
        simulatedReserved = $reservedRoot
        trustedResults = $trustedResultsRoot
        previousRunOutput = $previousOutputRoot
        simulatedCredential = $credentialRoot
    }
    traversalPath = 'C:\P21Input\..\simulated-reserved\reserved-sentinel.txt'
    uncPath = "\\$hostName\C$\$($reservedSentinelPath.Substring(3))"
    junctionTarget = $trustedConfigRoot
    hostOnlyExecutablePath = $hostOnlyExecutablePath
    hostEndpointAddress = $network.hostAddress
    hostEndpointPort = $endpointPort
    lanAddress = $network.gatewayAddress
    allowedEnvironmentNames = @(
        'ComSpec',
        'PATH',
        'PATHEXT',
        'PSExecutionPolicyPreference',
        'PSModulePath',
        'SystemRoot',
        'TEMP',
        'TMP',
        'windir'
    )
}
$probeConfig | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 (Join-Path $inputRoot 'probe-config.json')

$manifestFiles = @(
    Get-ChildItem -File -Recurse $inputRoot |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                relativePath = $_.FullName.Substring($inputRoot.Length + 1)
                sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
            }
        }
)
[ordered]@{
    wsbVersion = (& $wsbPath --version | Out-String).Trim()
    toolchain = [ordered]@{
        powershellVersion = $PSVersionTable.PSVersion.ToString()
        powershellSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath "$PSHOME\powershell.exe").Hash
        windowsSandboxSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath "$env:WINDIR\System32\WindowsSandbox.exe").Hash
    }
    hostHarnessFiles = @(
        [ordered]@{
            relativePath = 'scripts/Invoke-P21IsolationProbe.ps1'
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $PSCommandPath).Hash
        },
        [ordered]@{
            relativePath = 'scripts/P21TcpSentinelServer.ps1'
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $tcpServerScript).Hash
        }
    )
    inputFiles = $manifestFiles
    mappings = @(
        [ordered]@{ host = $inputRoot; guest = 'C:\P21Input'; readOnly = $true },
        [ordered]@{ host = '<phase-specific-output>'; guest = 'C:\P21Output'; readOnly = $false }
    )
    network = 'Disable'
    redirections = 'Disable'
} | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 $manifestPath

$protectedPaths = @($configSentinelPath, $reservedSentinelPath, $resultSentinelPath, $previousOutputSentinelPath, $credentialSentinelPath, $internalPath)
$hashesBefore = [ordered]@{}
foreach ($path in $protectedPaths) {
    $hashesBefore[$path] = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
}

$env:SIMULATED_HOST_CREDENTIAL = $credentialSentinel

try {
    $endpointProcess = Start-Process -FilePath powershell.exe `
        -ArgumentList @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', $tcpServerScript,
            '-Port', $endpointPort,
            '-LogPath', $endpointLogPath,
            '-ReadyPath', $endpointReadyPath
        ) `
        -PassThru `
        -WindowStyle Hidden

    if (-not (Wait-ForFile -Path $endpointReadyPath -TimeoutSeconds 10)) {
        throw 'Endpoint TCP host non pronto.'
    }

    $controlClient = [Net.Sockets.TcpClient]::new()
    $controlClient.Connect('127.0.0.1', $endpointPort)
    $controlBytes = [Text.Encoding]::ASCII.GetBytes('CONTROL')
    $controlClient.GetStream().Write($controlBytes, 0, $controlBytes.Length)
    $controlClient.Dispose()
    Start-Sleep -Milliseconds 500
    $controlConnectionCount = @(Get-Content $endpointLogPath -ErrorAction SilentlyContinue).Count
    if ($controlConnectionCount -ne 1) {
        throw "Controllo positivo endpoint non valido: $controlConnectionCount connessioni."
    }

    $mainSandbox = Start-EmptySandbox -OutputRoot $mainOutputRoot
    $mainCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\P21Input\project\Run-P21GuestProbe.ps1'
    $mainExec = Invoke-WsbExec -Id $mainSandbox.id -Command $mainCommand -TimeoutSeconds 120
    Stop-Sandbox -Id $mainSandbox.id

    $buildReport = Get-Content -Raw (Join-Path $mainOutputRoot 'build-test-report.json') | ConvertFrom-Json
    $guestSummary = Get-Content -Raw (Join-Path $mainOutputRoot 'guest-summary.json') | ConvertFrom-Json
    $parentReport = Get-Content -Raw (Join-Path $mainOutputRoot 'parent-report.json') | ConvertFrom-Json
    $childReport = Get-Content -Raw (Join-Path $mainOutputRoot 'child-report.json') | ConvertFrom-Json
    $parentChecks = Test-AllAttemptResults -Report $parentReport
    $childChecks = Test-AllAttemptResults -Report $childReport

    $timeoutSandbox = Start-EmptySandbox -OutputRoot $timeoutOutputRoot
    $timeoutCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\P21Input\project\Timeout-Probe.ps1'
    $timeoutExecProcess = Start-WsbExecProcess -Id $timeoutSandbox.id -Command $timeoutCommand
    $timeoutStarted = Wait-ForFile -Path (Join-Path $timeoutOutputRoot 'timeout-started.txt') -TimeoutSeconds 30
    if (-not $timeoutStarted) {
        throw 'Il processo di timeout guest non ha prodotto il marker iniziale.'
    }
    Start-Sleep -Seconds 2
    Stop-Sandbox -Id $timeoutSandbox.id
    $timeoutExecExited = $timeoutExecProcess.WaitForExit(15000)
    if (-not $timeoutExecExited) {
        $timeoutExecProcess.Kill()
    }
    $timeoutExecProcess.Dispose()
    $timeoutIdStillListed = @(((& $wsbPath list --raw | ConvertFrom-Json).WindowsSandboxEnvironments) | Where-Object Id -eq $timeoutSandbox.id).Count -gt 0

    $persistenceSandbox = Start-EmptySandbox -OutputRoot $persistenceOutputRoot
    $persistenceCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\P21Input\project\Check-Persistence.ps1'
    $persistenceExec = Invoke-WsbExec -Id $persistenceSandbox.id -Command $persistenceCommand -TimeoutSeconds 60
    Stop-Sandbox -Id $persistenceSandbox.id
    $persistenceReport = Get-Content -Raw (Join-Path $persistenceOutputRoot 'persistence-report.json') | ConvertFrom-Json

    Start-Sleep -Seconds 3
    $endpointConnectionCount = @(Get-Content $endpointLogPath -ErrorAction SilentlyContinue).Count

    $hashesAfter = [ordered]@{}
    $protectedIntegrity = $true
    foreach ($path in $protectedPaths) {
        $hashesAfter[$path] = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
        if ($hashesAfter[$path] -ne $hashesBefore[$path]) {
            $protectedIntegrity = $false
        }
    }

    $outputText = @(
        Get-ChildItem -File -Recurse $mainOutputRoot, $timeoutOutputRoot, $persistenceOutputRoot |
            ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName -ErrorAction SilentlyContinue }
    ) -join "`n"
    $protectedSentinelsDisclosed = @($protectedSentinels | Where-Object { $outputText.Contains($_) }).Count -gt 0
    $hostOnlyExecuted = Test-Path -LiteralPath $hostOnlyExecutionMarkerPath

    $allParentChecksPassed = @($parentChecks.Values | Where-Object { -not $_ }).Count -eq 0
    $allChildChecksPassed = @($childChecks.Values | Where-Object { -not $_ }).Count -eq 0
    $finalRunningCount = @(((& $wsbPath list --raw | ConvertFrom-Json).WindowsSandboxEnvironments)).Count

    $hostChecks = [ordered]@{
        mainExecExitCodeZero = ($mainExec.exitCode -eq 0)
        buildSucceeded = [bool]$buildReport.buildSucceeded
        testsSucceeded = [bool]$buildReport.testSucceeded
        guestSummaryPresent = ($null -ne $guestSummary)
        parentChecksPassed = $allParentChecksPassed
        childChecksPassed = $allChildChecksPassed
        childProcessObserved = ([bool]$guestSummary.childProcessStarted -and $guestSummary.childExitCode -eq 0)
        endpointPositiveControlSucceeded = ($controlConnectionCount -eq 1)
        endpointReceivedNoSandboxRequests = ($endpointConnectionCount -eq 1)
        protectedFilesUnchanged = $protectedIntegrity
        protectedSentinelsNotDisclosed = (-not $protectedSentinelsDisclosed)
        hostOnlyExecutableNotRun = (-not $hostOnlyExecuted)
        timeoutProbeStarted = $timeoutStarted
        timeoutStoppedWholeSandbox = (-not $timeoutIdStillListed)
        timeoutExecProcessExited = $timeoutExecExited
        persistenceExecExitCodeZero = ($persistenceExec.exitCode -eq 0)
        previousInternalMarkerAbsent = [bool]$persistenceReport.previousMarkerAbsent
        noRunningSandboxesRemain = ($finalRunningCount -eq 0)
        configurationsHashed = (-not [string]::IsNullOrWhiteSpace((Get-StringSha256 $mainSandbox.configuration)) -and
            -not [string]::IsNullOrWhiteSpace((Get-StringSha256 $timeoutSandbox.configuration)) -and
            -not [string]::IsNullOrWhiteSpace((Get-StringSha256 $persistenceSandbox.configuration)))
    }

    [ordered]@{
        runId = Split-Path -Leaf $runRoot
        guestOutputDirectories = [ordered]@{
            main = $mainOutputRoot
            timeout = $timeoutOutputRoot
            persistence = $persistenceOutputRoot
        }
        hostEvidenceDirectory = $hostEvidenceRoot
        hostChecks = $hostChecks
        parentChecks = $parentChecks
        childChecks = $childChecks
        sandboxConfigurationHashes = [ordered]@{
            main = Get-StringSha256 $mainSandbox.configuration
            timeout = Get-StringSha256 $timeoutSandbox.configuration
            persistence = Get-StringSha256 $persistenceSandbox.configuration
        }
        endpointConnectionCount = $endpointConnectionCount
        protectedHashesBefore = $hashesBefore
        protectedHashesAfter = $hashesAfter
        limits = @(
            'The test proves behavior only for the observed Windows Sandbox and wsb versions.',
            'The trusted guest wrapper reduces the environment to the recorded technical allowlist before build, tests, parent and child probes.',
            'Failure to reach public addresses is combined with adapter state and the host endpoint control; no single network attempt proves the whole policy.'
        )
    } | ConvertTo-Json -Depth 20 | Set-Content -Encoding utf8 $hostReportPath

    if (@($hostChecks.Values | Where-Object { -not $_ }).Count -gt 0) {
        throw "Una o più verifiche host non sono state superate. Vedere $hostReportPath"
    }

    Write-Output "RUN_ROOT=$runRoot"
    Write-Output "GUEST_OUTPUT_MAIN=$mainOutputRoot"
    Write-Output "HOST_EVIDENCE=$hostEvidenceRoot"
}
finally {
    Remove-Item Env:SIMULATED_HOST_CREDENTIAL -ErrorAction SilentlyContinue
    foreach ($sandboxId in @($activeSandboxIds)) {
        & $wsbPath stop --id $sandboxId --raw | Out-Null
    }
    if ($endpointProcess -and -not $endpointProcess.HasExited) {
        $endpointProcess.Kill()
        $endpointProcess.WaitForExit(5000) | Out-Null
    }
    if ($endpointProcess) {
        $endpointProcess.Dispose()
    }
}
