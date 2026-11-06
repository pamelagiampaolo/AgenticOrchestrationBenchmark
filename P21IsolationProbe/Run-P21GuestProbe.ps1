[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$inputRoot = 'C:\P21Input'
$outputRoot = 'C:\P21Output'
$workRoot = 'C:\P21Work'
$projectRoot = Join-Path $workRoot 'project'
$configPath = Join-Path $workRoot 'probe-config.json'
$buildReportPath = Join-Path $outputRoot 'build-test-report.json'
$guestSummaryPath = Join-Path $outputRoot 'guest-summary.json'

$allowedEnvironmentNames = @(
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
$environmentSnapshot = @{}
foreach ($entry in Get-ChildItem Env:) {
    $environmentSnapshot[$entry.Name] = $entry.Value
}
foreach ($entry in Get-ChildItem Env:) {
    if ($entry.Name -notin $allowedEnvironmentNames) {
        Remove-Item -LiteralPath "Env:$($entry.Name)"
    }
}
foreach ($name in $allowedEnvironmentNames) {
    if ($environmentSnapshot.ContainsKey($name)) {
        Set-Item -LiteralPath "Env:$name" -Value $environmentSnapshot[$name]
    }
}

New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
Copy-Item -Path (Join-Path $inputRoot 'project') -Destination $projectRoot -Recurse -Force
Copy-Item -Path (Join-Path $inputRoot 'probe-config.json') -Destination $configPath -Force

$sourcePath = Join-Path $projectRoot 'MinimalCalculator.cs'
$assemblyPath = Join-Path $workRoot 'P21IsolationProbe.dll'
$buildSucceeded = $false
$testSucceeded = $false
$buildErrorType = $null

try {
    Add-Type -Path $sourcePath -OutputAssembly $assemblyPath -OutputType Library
    $buildSucceeded = Test-Path -LiteralPath $assemblyPath
    $assembly = [Reflection.Assembly]::LoadFile($assemblyPath)
    $calculatorType = $assembly.GetType('P21IsolationProbe.MinimalCalculator', $true)
    $result = $calculatorType.GetMethod('Add').Invoke($null, @(2, 3))
    $testSucceeded = ($result -eq 5)
}
catch {
    $buildErrorType = $_.Exception.GetType().FullName
}

[ordered]@{
    toolchain = "Windows PowerShell $($PSVersionTable.PSVersion) Add-Type"
    buildAttempted = $true
    buildSucceeded = $buildSucceeded
    testAttempted = $true
    testSucceeded = $testSucceeded
    errorType = $buildErrorType
    environmentAllowlist = $allowedEnvironmentNames
} | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 $buildReportPath

if (-not $buildSucceeded -or -not $testSucceeded) {
    exit 10
}

$parentReportPath = Join-Path $outputRoot 'parent-report.json'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot 'Test-Isolation.ps1') `
    -Role parent `
    -ConfigPath $configPath `
    -ReportPath $parentReportPath
$parentExitCode = $LASTEXITCODE

$parentReport = if (Test-Path $parentReportPath) {
    Get-Content -Raw $parentReportPath | ConvertFrom-Json
} else {
    $null
}
$childReportPath = Join-Path $outputRoot 'child-report.json'
$childReport = if (Test-Path $childReportPath) {
    Get-Content -Raw $childReportPath | ConvertFrom-Json
} else {
    $null
}

[ordered]@{
    buildSucceeded = $buildSucceeded
    testSucceeded = $testSucceeded
    parentExitCode = $parentExitCode
    parentReportPresent = ($null -ne $parentReport)
    childReportPresent = ($null -ne $childReport)
    childProcessStarted = if ($parentReport) { $parentReport.childProcessStarted } else { $false }
    childExitCode = if ($parentReport) { $parentReport.childExitCode } else { $null }
} | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 $guestSummaryPath

if ($parentExitCode -ne 0 -or $null -eq $childReport) {
    exit 11
}

exit 0
