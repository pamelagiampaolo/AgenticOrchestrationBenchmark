[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{40}$|^[0-9a-fA-F]{64}$')]
    [string]$Commit,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$resolvedCommit = (& git -C $repositoryRoot rev-parse --verify "$Commit^{commit}").Trim()
if ($LASTEXITCODE -ne 0 -or $resolvedCommit -ne $Commit.ToLowerInvariant()) {
    throw "Il commit richiesto non risolve esattamente a '$Commit'."
}

$tree = (& git -C $repositoryRoot rev-parse "$resolvedCommit^{tree}").Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Impossibile risolvere il tree del commit '$resolvedCommit'."
}

$scopes = [ordered]@{
    benchmark = @('benchmark/BenchmarkTarget')
    publicSpecifications = @('docs/benchmark/task-specifications')
    instructions = @('src/AgenticExperimentRunner/Instructions')
    trustedHarness = @(
        'AgenticOrchestrationBenchmark.slnx',
        'global.json',
        'src/AgenticExperimentRunner',
        'src/AgentWorkspaceMcpServer'
    )
}

$selected = @{}
foreach ($scope in $scopes.GetEnumerator()) {
    $paths = @(& git -C $repositoryRoot ls-tree -r --name-only $resolvedCommit -- @($scope.Value))
    if ($LASTEXITCODE -ne 0) {
        throw "Inventario Git non riuscito per la categoria '$($scope.Key)'."
    }

    foreach ($path in $paths) {
        if ($selected.ContainsKey($path)) {
            # Gli scope più specifici precedono quelli generali: per esempio le
            # istruzioni restano classificate come tali anche se sono sotto src/.
            continue
        }

        $selected[$path] = $scope.Key
    }
}

if ($selected.Count -eq 0) {
    throw 'La selezione del manifest è vuota.'
}

$sortedPaths = [string[]]$selected.Keys
[Array]::Sort($sortedPaths, [StringComparer]::Ordinal)

$reserved = @($selected.Keys | Where-Object { $_ -match '(^|/)tests/reserved(/|$)' })
if ($reserved.Count -gt 0) {
    throw "La selezione contiene test riservati: $($reserved -join ', ')."
}

$outputCandidate = if ([IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
}
else {
    Join-Path $repositoryRoot $OutputPath
}
$fullOutputPath = [IO.Path]::GetFullPath($outputCandidate)
if (Test-Path -LiteralPath $fullOutputPath) {
    throw "Il file di output esiste già: '$fullOutputPath'."
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('frozen-source-manifest-' + [guid]::NewGuid().ToString('N'))
$archivePath = Join-Path $temporaryRoot 'source.tar'
$extractionRoot = Join-Path $temporaryRoot 'source'
New-Item -ItemType Directory -Path $temporaryRoot, $extractionRoot | Out-Null

try {
    $archiveArguments = @(
        '-C', $repositoryRoot,
        '-c', 'core.autocrlf=false',
        '-c', 'core.eol=lf',
        'archive', '--format=tar', "--output=$archivePath", $resolvedCommit, '--'
    ) + $sortedPaths
    & git @archiveArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Estrazione Git non riuscita per il commit '$resolvedCommit'."
    }

    & tar -xf $archivePath -C $extractionRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Estrazione dell'archivio non riuscita: '$archivePath'."
    }

    $entries = foreach ($path in $sortedPaths) {
        $extractedPath = Join-Path $extractionRoot $path.Replace('/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $extractedPath -PathType Leaf)) {
            throw "Il blob selezionato non è stato estratto: '$path'."
        }

        $item = Get-Item -LiteralPath $extractedPath
        [ordered]@{
            category = $selected[$path]
            path = $path
            length = $item.Length
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $extractedPath).Hash.ToLowerInvariant()
        }
    }

    $canonicalLines = foreach ($entry in $entries) {
        "$($entry.category)`0$($entry.path)`0$($entry.length)`0$($entry.sha256)`n"
    }
    $canonicalBytes = [Text.UTF8Encoding]::new($false).GetBytes(($canonicalLines -join ''))
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $aggregateSha256 = ([BitConverter]::ToString(
            $sha256.ComputeHash($canonicalBytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }

    $manifest = [ordered]@{
        schemaVersion = 1
        sourceCommit = $resolvedCommit
        sourceTree = $tree
        aggregateAlgorithm = 'sha256(category NUL path NUL length NUL sha256 LF), paths ordered ordinally'
        aggregateSha256 = $aggregateSha256
        fileCount = $entries.Count
        scopes = $scopes
        exclusions = @(
            'compiled executables and generated runtime configurations (frozen separately)',
            'toolchain (frozen separately)',
            'trusted evaluator and reserved tests (frozen separately and never exposed to agents)',
            'build outputs, node_modules, test results and repository history'
        )
        files = $entries
    }

    $outputDirectory = Split-Path -Parent $fullOutputPath
    if ($outputDirectory) {
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    }
    $json = ($manifest | ConvertTo-Json -Depth 10).Replace("`r`n", "`n") + "`n"
    [IO.File]::WriteAllText($fullOutputPath, $json, [Text.UTF8Encoding]::new($false))
    Write-Output "MANIFEST_PATH=$fullOutputPath"
    Write-Output "FILE_COUNT=$($entries.Count)"
    Write-Output "AGGREGATE_SHA256=$aggregateSha256"
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
