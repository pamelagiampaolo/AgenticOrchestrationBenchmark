[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$benchmarkRoot = Join-Path $repositoryRoot 'BenchmarkTarget'
$guestScript = Join-Path $repositoryRoot 'experiments\legacy-isolation-probes\NodeBaselineSandboxProbe\Run-NodeBaselineGuest.ps1'
$wsbPath = (Get-Command wsb.exe -ErrorAction Stop).Source
$nodePath = (Get-Command node.exe -ErrorAction Stop).Source
$npmPath = (Get-Command npm.cmd -ErrorAction Stop).Source
$nodeRuntimeRoot = Split-Path -Parent $nodePath
$runRoot = Join-Path ([IO.Path]::GetTempPath()) ("node-baseline-sandbox-" + [guid]::NewGuid().ToString('N'))
$inputRoot = Join-Path $runRoot 'authorized-input'
$inputBenchmarkRoot = Join-Path $inputRoot 'BenchmarkTarget'
$inputRuntimeRoot = Join-Path $inputRoot 'runtime'
$outputRoot = Join-Path $runRoot 'sandbox-output'
$hostEvidenceRoot = Join-Path $runRoot 'host-evidence'
$manifestPath = Join-Path $hostEvidenceRoot 'input-manifest.json'
$hostReportPath = Join-Path $hostEvidenceRoot 'host-verification.json'
$archivePath = Join-Path $runRoot 'benchmark.zip'
$sandboxId = $null

function Get-TreeManifest {
    param([string]$Root)

    return @(
        Get-ChildItem -LiteralPath $Root -File -Recurse |
            Sort-Object FullName |
            ForEach-Object {
                [ordered]@{
                    relativePath = $_.FullName.Substring($Root.Length + 1)
                    length = $_.Length
                    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
                }
            }
    )
}

function Get-ManifestDigest {
    param($Manifest)

    $text = ($Manifest | ConvertTo-Json -Depth 5 -Compress)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).Replace('-', '')
    }
    finally {
        $algorithm.Dispose()
    }
}

function ConvertTo-QuotedProcessArgument {
    param([string]$Value)

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Invoke-WsbExec {
    param(
        [string]$Id,
        [string]$Command,
        [int]$TimeoutSeconds
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

New-Item -ItemType Directory -Path $inputRoot, $inputRuntimeRoot, $outputRoot, $hostEvidenceRoot -Force | Out-Null

$baselineCommit = (& git -C $repositoryRoot rev-parse HEAD | Out-String).Trim()
$branch = (& git -C $repositoryRoot branch --show-current | Out-String).Trim()
$benchmarkDiff = & git -C $repositoryRoot status --porcelain --untracked-files=all -- BenchmarkTarget
if ($LASTEXITCODE -ne 0 -or $benchmarkDiff) {
    throw 'BenchmarkTarget non corrisponde a una copia pulita del commit sorgente.'
}

& git -C $repositoryRoot archive --format=zip --output=$archivePath $baselineCommit BenchmarkTarget
if ($LASTEXITCODE -ne 0) {
    throw 'Creazione archivio della baseline non riuscita.'
}
Expand-Archive -LiteralPath $archivePath -DestinationPath $inputRoot
Remove-Item -LiteralPath $archivePath

Copy-Item -LiteralPath (Join-Path $benchmarkRoot 'node_modules') `
    -Destination (Join-Path $inputBenchmarkRoot 'node_modules') `
    -Recurse `
    -Force
Copy-Item -LiteralPath $nodePath -Destination (Join-Path $inputRuntimeRoot 'node.exe')
Copy-Item -LiteralPath $npmPath -Destination (Join-Path $inputRuntimeRoot 'npm.cmd')
Copy-Item -LiteralPath (Join-Path $nodeRuntimeRoot 'node_modules\npm') `
    -Destination (Join-Path $inputRuntimeRoot 'node_modules\npm') `
    -Recurse `
    -Force
$copiedNpmConfiguration = Join-Path $inputRuntimeRoot 'node_modules\npm\.npmrc'
if (Test-Path -LiteralPath $copiedNpmConfiguration) {
    Remove-Item -LiteralPath $copiedNpmConfiguration
}
Copy-Item -LiteralPath $guestScript -Destination (Join-Path $inputRoot 'Run-NodeBaselineGuest.ps1')

$forbiddenFiles = @(
    Get-ChildItem -LiteralPath $inputRoot -Force -Recurse |
        Where-Object {
            $_.Name -in @('.git', '.gitconfig', '.npmrc', '.env') -or
            $_.FullName -match '[\\/]tests[\\/]reserved(?:[\\/]|$)'
        }
)
if ($forbiddenFiles.Count -gt 0) {
    throw "Il pacchetto contiene elementi vietati: $($forbiddenFiles.FullName -join ', ')"
}

$package = Get-Content -Raw -LiteralPath (Join-Path $inputBenchmarkRoot 'package.json') | ConvertFrom-Json
$nodeVersion = (& $nodePath --version | Out-String).Trim()
$npmVersion = (& $npmPath --version | Out-String).Trim()
$inputManifestBefore = Get-TreeManifest -Root $inputRoot
$inputDigestBefore = Get-ManifestDigest -Manifest $inputManifestBefore

[ordered]@{
    baselineSourceCommit = $baselineCommit
    branch = $branch
    definitiveExperimentalBaselineDesignated = $false
    nodeVersion = $nodeVersion
    npmVersion = $npmVersion
    packageScripts = [ordered]@{
        build = $package.scripts.build
        typecheck = $package.scripts.typecheck
        test = $package.scripts.test
    }
    dependencyVersions = [ordered]@{
        typescript = $package.devDependencies.typescript
        vitest = $package.devDependencies.vitest
    }
    fileCount = $inputManifestBefore.Count
    treeSha256 = $inputDigestBefore
    files = $inputManifestBefore
    exclusionsVerified = @('.git', '.gitconfig', '.npmrc', '.env', 'tests/reserved')
} | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 -LiteralPath $manifestPath

$escapedInput = [Security.SecurityElement]::Escape($inputRoot)
$escapedOutput = [Security.SecurityElement]::Escape($outputRoot)
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
      <SandboxFolder>C:\BaselineInput</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$escapedOutput</HostFolder>
      <SandboxFolder>C:\BaselineOutput</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
</Configuration>
"@

try {
    $startText = (& $wsbPath start --config $configuration --raw 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "wsb start non riuscito: $startText"
    }
    $sandboxId = [string](($startText | ConvertFrom-Json).Id)
    if ([string]::IsNullOrWhiteSpace($sandboxId)) {
        throw 'wsb start non ha restituito un ID.'
    }

    $exec = Invoke-WsbExec `
        -Id $sandboxId `
        -Command 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\BaselineInput\Run-NodeBaselineGuest.ps1' `
        -TimeoutSeconds 180

    & $wsbPath stop --id $sandboxId --raw | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Arresto della Sandbox non riuscito.'
    }
    $sandboxId = $null

    $guestResultPath = Join-Path $outputRoot 'guest-result.json'
    if (-not (Test-Path -LiteralPath $guestResultPath)) {
        throw 'Il guest non ha prodotto il report atteso.'
    }
    $guestResult = Get-Content -Raw -LiteralPath $guestResultPath | ConvertFrom-Json
    $inputManifestAfter = Get-TreeManifest -Root $inputRoot
    $inputDigestAfter = Get-ManifestDigest -Manifest $inputManifestAfter
    $activeCount = @(((& $wsbPath list --raw | ConvertFrom-Json).WindowsSandboxEnvironments)).Count

    $checks = [ordered]@{
        sourceCommitRecorded = (-not [string]::IsNullOrWhiteSpace($baselineCommit))
        forbiddenMaterialAbsent = ($forbiddenFiles.Count -eq 0)
        nodeVersionMatches = ($guestResult.nodeVersion -eq $nodeVersion)
        npmVersionMatches = ($guestResult.npmVersion -eq $npmVersion)
        buildPassed = ($guestResult.build.exitCode -eq 0)
        typecheckPassed = ($guestResult.typecheck.exitCode -eq 0)
        publicSuitePassed = ($guestResult.test.exitCode -eq 0)
        exactlyTwentySevenPassed = ($guestResult.totalTests -eq 27 -and $guestResult.passedTests -eq 27)
        noFailedOrPendingTests = ($guestResult.failedTests -eq 0 -and $guestResult.pendingTests -eq 0)
        networkAdapterAbsent = [bool]$guestResult.noUpNonLoopbackAdapters
        inputIntegrityPreserved = ($inputDigestBefore -eq $inputDigestAfter)
        sandboxExecExitCodeZero = ($exec.exitCode -eq 0)
        sandboxStopped = ($activeCount -eq 0)
        guestAndHostEvidenceSeparated = (-not $hostEvidenceRoot.StartsWith($outputRoot, [StringComparison]::OrdinalIgnoreCase))
    }

    [ordered]@{
        baselineSourceCommit = $baselineCommit
        branch = $branch
        nodeVersion = $nodeVersion
        npmVersion = $npmVersion
        commands = @('npm run build', 'npm run typecheck', 'npm test -- --reporter=json --outputFile=public-test-report.json')
        sandboxConfiguration = $configuration
        inputTreeSha256Before = $inputDigestBefore
        inputTreeSha256After = $inputDigestAfter
        guestOutputDirectory = $outputRoot
        hostEvidenceDirectory = $hostEvidenceRoot
        guestResult = $guestResult
        checks = $checks
        limits = @(
            'The current clean HEAD is the source commit for this probe, not a designation of the definitive experimental baseline.',
            'The packaged node_modules tree is the already-installed dependency tree; npm ci was not run because networking is disabled.',
            'The result applies to the observed Windows Sandbox, Node, npm and dependency versions.'
        )
    } | ConvertTo-Json -Depth 12 | Set-Content -Encoding utf8 -LiteralPath $hostReportPath

    if (@($checks.Values | Where-Object { -not $_ }).Count -gt 0) {
        throw "Una o più verifiche non sono state superate. Vedere $hostReportPath"
    }

    Write-Output "RUN_ROOT=$runRoot"
    Write-Output "GUEST_OUTPUT=$outputRoot"
    Write-Output "HOST_EVIDENCE=$hostEvidenceRoot"
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($sandboxId)) {
        & $wsbPath stop --id $sandboxId --raw | Out-Null
    }
}
