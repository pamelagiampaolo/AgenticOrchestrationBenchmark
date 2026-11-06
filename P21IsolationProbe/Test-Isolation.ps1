[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('parent', 'child')]
    [string]$Role,

    [Parameter(Mandatory)]
    [string]$ConfigPath,

    [Parameter(Mandatory)]
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
$config = Get-Content -Raw -LiteralPath $ConfigPath | ConvertFrom-Json
$attempts = [ordered]@{}

function Test-ReadDenied {
    param([string]$Path)

    try {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        $stream.Dispose()
        return [ordered]@{ attempted = $true; denied = $false; errorType = $null }
    }
    catch {
        return [ordered]@{ attempted = $true; denied = $true; errorType = $_.Exception.GetType().FullName }
    }
}

function Test-DirectoryEnumerationDenied {
    param([string]$Path)

    try {
        [IO.Directory]::EnumerateFileSystemEntries($Path) | Select-Object -First 1 | Out-Null
        return [ordered]@{ attempted = $true; denied = $false; errorType = $null }
    }
    catch {
        return [ordered]@{ attempted = $true; denied = $true; errorType = $_.Exception.GetType().FullName }
    }
}

function Test-TcpDenied {
    param(
        [string]$Address,
        [int]$Port
    )

    $client = [Net.Sockets.TcpClient]::new()
    try {
        $async = $client.BeginConnect($Address, $Port, $null, $null)
        $completed = $async.AsyncWaitHandle.WaitOne(2000)
        if (-not $completed) {
            return [ordered]@{ attempted = $true; denied = $true; outcome = 'timeout' }
        }

        $client.EndConnect($async)
        return [ordered]@{ attempted = $true; denied = $false; outcome = 'connected' }
    }
    catch {
        return [ordered]@{ attempted = $true; denied = $true; outcome = $_.Exception.GetType().FullName }
    }
    finally {
        $client.Dispose()
    }
}

$internalPath = Join-Path $config.inputRoot 'internal.txt'
try {
    $internalValue = Get-Content -Raw -LiteralPath $internalPath
    $attempts.internalRead = [ordered]@{
        attempted = $true
        succeeded = ($internalValue -eq $config.internalExpectedValue)
    }
}
catch {
    $attempts.internalRead = [ordered]@{ attempted = $true; succeeded = $false; errorType = $_.Exception.GetType().FullName }
}

$scratchRoot = Join-Path $config.workRoot "scratch-$Role"
try {
    New-Item -ItemType Directory -Path $scratchRoot -Force | Out-Null
    $scratchPath = Join-Path $scratchRoot 'scratch.txt'
    Set-Content -NoNewline -Encoding utf8 $scratchPath "scratch-$Role"
    $scratchRead = Get-Content -Raw $scratchPath
    Remove-Item -LiteralPath $scratchPath -Force
    $attempts.scratchLifecycle = [ordered]@{
        attempted = $true
        succeeded = ($scratchRead -eq "scratch-$Role" -and -not (Test-Path $scratchPath))
    }
}
catch {
    $attempts.scratchLifecycle = [ordered]@{ attempted = $true; succeeded = $false; errorType = $_.Exception.GetType().FullName }
}

try {
    $outputProbePath = Join-Path $config.outputRoot "$Role-output-write.txt"
    Set-Content -NoNewline -Encoding utf8 $outputProbePath "output-$Role"
    $attempts.outputWrite = [ordered]@{ attempted = $true; succeeded = (Test-Path $outputProbePath) }
}
catch {
    $attempts.outputWrite = [ordered]@{ attempted = $true; succeeded = $false; errorType = $_.Exception.GetType().FullName }
}

try {
    Set-Content -NoNewline -Encoding utf8 $internalPath 'UNEXPECTED_INPUT_WRITE'
    $attempts.inputWrite = [ordered]@{ attempted = $true; denied = $false; errorType = $null }
}
catch {
    $attempts.inputWrite = [ordered]@{ attempted = $true; denied = $true; errorType = $_.Exception.GetType().FullName }
}

$protectedReads = [ordered]@{}
foreach ($entry in $config.protectedFiles.PSObject.Properties) {
    $protectedReads[$entry.Name] = Test-ReadDenied -Path ([string]$entry.Value)
}
$attempts.protectedReads = $protectedReads

$protectedListings = [ordered]@{}
foreach ($entry in $config.protectedDirectories.PSObject.Properties) {
    $protectedListings[$entry.Name] = Test-DirectoryEnumerationDenied -Path ([string]$entry.Value)
}
$attempts.protectedDirectoryListings = $protectedListings

$attempts.traversalRead = Test-ReadDenied -Path $config.traversalPath
$attempts.uncRead = Test-ReadDenied -Path $config.uncPath

$junctionPath = Join-Path $config.workRoot "escape-link-$Role"
$junctionProcess = Start-Process -FilePath cmd.exe `
    -ArgumentList @('/d', '/c', 'mklink', '/J', $junctionPath, $config.junctionTarget) `
    -Wait `
    -PassThru `
    -WindowStyle Hidden
$junctionCreated = ($junctionProcess.ExitCode -eq 0 -and (Test-Path $junctionPath))
$junctionRead = Test-ReadDenied -Path (Join-Path $junctionPath 'config-sentinel.txt')
$attempts.junctionEscape = [ordered]@{
    attempted = $true
    junctionCreated = $junctionCreated
    readDenied = $junctionRead.denied
    readErrorType = $junctionRead.errorType
}

try {
    $hostProcess = Start-Process -FilePath $config.hostOnlyExecutablePath -PassThru -WindowStyle Hidden
    $hostProcess.WaitForExit(3000) | Out-Null
    $attempts.hostProcessLaunch = [ordered]@{ attempted = $true; denied = $false; errorType = $null }
}
catch {
    $attempts.hostProcessLaunch = [ordered]@{ attempted = $true; denied = $true; errorType = $_.Exception.GetType().FullName }
}

$observedEnvironmentNames = @(Get-ChildItem Env: | Select-Object -ExpandProperty Name | Sort-Object)
$unexpectedEnvironmentNames = @($observedEnvironmentNames | Where-Object { $_ -notin $config.allowedEnvironmentNames })
$missingEnvironmentNames = @($config.allowedEnvironmentNames | Where-Object { $_ -notin $observedEnvironmentNames })
$attempts.sensitiveEnvironment = [ordered]@{
    attempted = $true
    exactAllowlist = ($unexpectedEnvironmentNames.Count -eq 0 -and $missingEnvironmentNames.Count -eq 0)
    observedNames = $observedEnvironmentNames
    unexpectedNames = $unexpectedEnvironmentNames
    missingNames = $missingEnvironmentNames
}

try {
    [Net.Dns]::GetHostAddresses('example.com') | Out-Null
    $attempts.publicDns = [ordered]@{ attempted = $true; denied = $false; outcome = 'resolved' }
}
catch {
    $attempts.publicDns = [ordered]@{ attempted = $true; denied = $true; outcome = $_.Exception.GetType().FullName }
}

$attempts.publicTcp = Test-TcpDenied -Address '1.1.1.1' -Port 443
$attempts.hostEndpointTcp = Test-TcpDenied -Address $config.hostEndpointAddress -Port $config.hostEndpointPort
$attempts.lanTcp = Test-TcpDenied -Address $config.lanAddress -Port $config.hostEndpointPort

$upNonLoopbackAdapters = @(
    [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() |
        Where-Object {
            $_.OperationalStatus -eq [Net.NetworkInformation.OperationalStatus]::Up -and
            $_.NetworkInterfaceType -ne [Net.NetworkInformation.NetworkInterfaceType]::Loopback
        } |
        Select-Object -ExpandProperty Name
)
$attempts.networkAdapters = [ordered]@{
    attempted = $true
    noUpNonLoopbackAdapters = ($upNonLoopbackAdapters.Count -eq 0)
    observedNames = $upNonLoopbackAdapters
}

$childExitCode = $null
$childStarted = $false
if ($Role -eq 'parent') {
    $childReportPath = Join-Path $config.outputRoot 'child-report.json'
    $childProcess = Start-Process -FilePath powershell.exe `
        -ArgumentList @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', $PSCommandPath,
            '-Role', 'child',
            '-ConfigPath', $ConfigPath,
            '-ReportPath', $childReportPath
        ) `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    $childStarted = $true
    $childExitCode = $childProcess.ExitCode
}

[ordered]@{
    role = $Role
    processId = $PID
    attempts = $attempts
    childProcessStarted = $childStarted
    childExitCode = $childExitCode
} | ConvertTo-Json -Depth 20 | Set-Content -Encoding utf8 $ReportPath

exit 0
