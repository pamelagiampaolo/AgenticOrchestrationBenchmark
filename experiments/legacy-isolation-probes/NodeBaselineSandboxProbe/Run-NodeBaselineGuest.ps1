[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$inputRoot = 'C:\BaselineInput'
$outputRoot = 'C:\BaselineOutput'
$workRoot = 'C:\BaselineWork'
$benchmarkRoot = Join-Path $workRoot 'benchmark\BenchmarkTarget'
$runtimeRoot = Join-Path $workRoot 'runtime'
$npmPath = Join-Path $runtimeRoot 'npm.cmd'
$resultPath = Join-Path $outputRoot 'guest-result.json'

Copy-Item -LiteralPath $inputRoot -Destination $workRoot -Recurse -Force

function Invoke-BenchmarkCommand {
    param(
        [string]$Name,
        [string[]]$Arguments
    )

    $stdoutPath = Join-Path $outputRoot "$Name.stdout.txt"
    $stderrPath = Join-Path $outputRoot "$Name.stderr.txt"
    $process = Start-Process -FilePath $npmPath `
        -ArgumentList $Arguments `
        -WorkingDirectory $benchmarkRoot `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -Wait `
        -PassThru `
        -NoNewWindow

    return [ordered]@{
        command = "npm $($Arguments -join ' ')"
        exitCode = $process.ExitCode
        stdoutFile = Split-Path -Leaf $stdoutPath
        stderrFile = Split-Path -Leaf $stderrPath
    }
}

$env:PATH = "$runtimeRoot;$env:WINDIR\System32;$env:WINDIR"
$env:npm_config_offline = 'true'
$env:npm_config_audit = 'false'
$env:npm_config_fund = 'false'
$env:npm_config_update_notifier = 'false'
$env:NODE_OPTIONS = '--no-network-family-autoselection'

$nodeVersion = (& (Join-Path $runtimeRoot 'node.exe') --version | Out-String).Trim()
$npmVersion = (& $npmPath --version | Out-String).Trim()
$build = Invoke-BenchmarkCommand -Name 'build' -Arguments @('run', 'build')
$typecheck = Invoke-BenchmarkCommand -Name 'typecheck' -Arguments @('run', 'typecheck')
$test = Invoke-BenchmarkCommand -Name 'test' -Arguments @(
    'test', '--', '--reporter=json', '--outputFile=public-test-report.json'
)

$testReportPath = Join-Path $benchmarkRoot 'public-test-report.json'
$testReport = if (Test-Path -LiteralPath $testReportPath) {
    Get-Content -Raw -LiteralPath $testReportPath | ConvertFrom-Json
} else {
    $null
}

$upNonLoopbackAdapters = @(
    [Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() |
        Where-Object {
            $_.OperationalStatus -eq [Net.NetworkInformation.OperationalStatus]::Up -and
            $_.NetworkInterfaceType -ne [Net.NetworkInformation.NetworkInterfaceType]::Loopback
        } |
        Select-Object -ExpandProperty Name
)

[ordered]@{
    nodeVersion = $nodeVersion
    npmVersion = $npmVersion
    build = $build
    typecheck = $typecheck
    test = $test
    testReportPresent = ($null -ne $testReport)
    totalTests = if ($null -ne $testReport) { $testReport.numTotalTests } else { $null }
    passedTests = if ($null -ne $testReport) { $testReport.numPassedTests } else { $null }
    failedTests = if ($null -ne $testReport) { $testReport.numFailedTests } else { $null }
    pendingTests = if ($null -ne $testReport) { $testReport.numPendingTests } else { $null }
    noUpNonLoopbackAdapters = ($upNonLoopbackAdapters.Count -eq 0)
    upNonLoopbackAdapterNames = $upNonLoopbackAdapters
} | ConvertTo-Json -Depth 10 | Set-Content -Encoding utf8 -LiteralPath $resultPath

if ($build.exitCode -ne 0 -or
    $typecheck.exitCode -ne 0 -or
    $test.exitCode -ne 0 -or
    $null -eq $testReport -or
    $testReport.numTotalTests -ne 27 -or
    $testReport.numPassedTests -ne 27 -or
    $testReport.numFailedTests -ne 0 -or
    $testReport.numPendingTests -ne 0 -or
    $upNonLoopbackAdapters.Count -ne 0) {
    exit 20
}

exit 0
