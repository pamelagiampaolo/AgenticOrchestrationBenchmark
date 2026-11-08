[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory)]
    [string]$SourceCommit,

    [Parameter(Mandatory)]
    [ValidatePattern('^T[1-6]$')]
    [string]$TaskId,

    [Parameter(Mandatory)]
    [string]$DestinationRoot,

    [Parameter(Mandatory)]
    [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'

function Assert-AbsolutePath {
    param(
        [string]$Path,
        [string]$ParameterName
    )

    $isDriveQualified = $Path -match '^[a-zA-Z]:[\\/]'
    $isUncQualified = $Path -match '^\\\\[^\\]+\\[^\\]+'
    if (-not $isDriveQualified -and -not $isUncQualified) {
        throw "$ParameterName deve essere un percorso assoluto."
    }
}

function Get-RelativePath {
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    $normalizedBase = [IO.Path]::GetFullPath($BasePath).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $baseUri = [Uri]::new($normalizedBase)
    $targetUri = [Uri]::new([IO.Path]::GetFullPath($TargetPath))
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace(
        '/',
        [IO.Path]::DirectorySeparatorChar)
}

function Assert-NoReparsePoints {
    param([string]$Root)

    $entries = @(
        Get-ChildItem -LiteralPath $Root -Force -Recurse |
            Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }
    )
    if ($entries.Count -ne 0) {
        throw "Il materiale preparato contiene collegamenti o reparse point non ammessi."
    }
}

function Copy-DirectoryContent {
    param(
        [string]$Source,
        [string]$Destination,
        [string[]]$ExcludedRelativePrefixes = @()
    )

    foreach ($sourceFile in Get-ChildItem -LiteralPath $Source -File -Recurse | Sort-Object FullName) {
        $relativePath = Get-RelativePath -BasePath $Source -TargetPath $sourceFile.FullName
        $isExcluded = @(
            $ExcludedRelativePrefixes |
                Where-Object {
                    $relativePath -eq $_ -or
                    $relativePath.StartsWith($_ + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
                }
        ).Count -ne 0
        if ($isExcluded) {
            continue
        }

        $destinationPath = Join-Path $Destination $relativePath
        $destinationDirectory = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
        Copy-Item -LiteralPath $sourceFile.FullName -Destination $destinationPath
    }
}

function Get-FileManifest {
    param(
        [string]$AreaRoot,
        [string]$RelativeTo
    )

    return @(
        Get-ChildItem -LiteralPath $AreaRoot -File -Recurse |
            Sort-Object FullName |
            ForEach-Object {
                [ordered]@{
                    path = (Get-RelativePath -BasePath $RelativeTo -TargetPath $_.FullName).Replace('\', '/')
                    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
                    bytes = $_.Length
                }
            }
    )
}

Assert-AbsolutePath -Path $RepositoryRoot -ParameterName 'RepositoryRoot'
Assert-AbsolutePath -Path $DestinationRoot -ParameterName 'DestinationRoot'
Assert-AbsolutePath -Path $ManifestPath -ParameterName 'ManifestPath'

$repositoryPath = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
$destinationPath = [IO.Path]::GetFullPath($DestinationRoot).TrimEnd('\', '/')
$manifestFilePath = [IO.Path]::GetFullPath($ManifestPath)
$destinationPrefix = $destinationPath + [IO.Path]::DirectorySeparatorChar

if (-not (Test-Path -LiteralPath (Join-Path $repositoryPath '.git'))) {
    throw "RepositoryRoot non identifica un worktree Git: '$repositoryPath'."
}
if (Test-Path -LiteralPath $destinationPath) {
    throw "La destinazione deve essere nuova: '$destinationPath'."
}
if ($manifestFilePath.StartsWith($destinationPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Il manifest fidato non può essere salvato dentro l’area accessibile all’agente.'
}

$manifestDirectory = Split-Path -Parent $manifestFilePath
if (Test-Path -LiteralPath $manifestFilePath) {
    throw "Il manifest esiste già: '$manifestFilePath'."
}
New-Item -ItemType Directory -Force -Path $manifestDirectory | Out-Null

$resolvedCommit = (& git -C $repositoryPath rev-parse --verify "$SourceCommit`^{commit}" 2>$null | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $resolvedCommit -notmatch '^[0-9a-fA-F]{40}$') {
    throw "Commit sorgente non valido o non disponibile: '$SourceCommit'."
}

$taskSpecificationRelativePath = "docs/benchmark/$TaskId-specifica-pubblica.md"
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("agent-workspace-source-" + [guid]::NewGuid().ToString('N'))
$archivePath = Join-Path $temporaryRoot 'source.zip'
$extractedRoot = Join-Path $temporaryRoot 'extracted'

try {
    New-Item -ItemType Directory -Path $temporaryRoot, $extractedRoot | Out-Null
    & git -C $repositoryPath archive --format=zip --output=$archivePath $resolvedCommit BenchmarkTarget $taskSpecificationRelativePath
    if ($LASTEXITCODE -ne 0) {
        throw "git archive non è riuscito per il commit '$resolvedCommit'."
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractedRoot
    Assert-NoReparsePoints -Root $extractedRoot

    $sourceBenchmark = Join-Path $extractedRoot 'BenchmarkTarget'
    $sourcePublicTests = Join-Path $sourceBenchmark 'tests\public'
    $sourceTaskSpecification = Join-Path $extractedRoot ($taskSpecificationRelativePath.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $sourcePublicTests -PathType Container)) {
        throw 'Il commit non contiene i test pubblici attesi.'
    }
    if (-not (Test-Path -LiteralPath $sourceTaskSpecification -PathType Leaf)) {
        throw "Il commit non contiene la specifica pubblica $TaskId."
    }

    $workspacePath = Join-Path $destinationPath 'workspace'
    $publicReferencePath = Join-Path $destinationPath 'public-reference'
    $agentTestsPath = Join-Path $destinationPath 'agent-tests'
    New-Item -ItemType Directory -Path $workspacePath, $publicReferencePath, $agentTestsPath | Out-Null

    Copy-DirectoryContent -Source $sourceBenchmark -Destination $workspacePath -ExcludedRelativePrefixes @('tests\public')
    Copy-DirectoryContent -Source $sourcePublicTests -Destination $publicReferencePath
    Copy-Item -LiteralPath $sourceTaskSpecification -Destination (Join-Path $destinationPath 'task-specification.md')

    Assert-NoReparsePoints -Root $destinationPath
    if (Test-Path -LiteralPath (Join-Path $destinationPath '.git')) {
        throw 'La copia agente contiene inaspettatamente metadati Git.'
    }

    $manifest = [ordered]@{
        schemaVersion = 1
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        sourceCommit = $resolvedCommit.ToLowerInvariant()
        taskId = $TaskId
        destinationRoot = $destinationPath
        files = [ordered]@{
            workspace = Get-FileManifest -AreaRoot $workspacePath -RelativeTo $destinationPath
            publicReference = Get-FileManifest -AreaRoot $publicReferencePath -RelativeTo $destinationPath
            taskSpecification = Get-FileManifest -AreaRoot (Join-Path $destinationPath 'task-specification.md') -RelativeTo $destinationPath
            agentTests = @()
        }
    }
    [IO.File]::WriteAllText(
        $manifestFilePath,
        ($manifest | ConvertTo-Json -Depth 10),
        [Text.UTF8Encoding]::new($false))

    Write-Output "WORKSPACE_ROOT=$destinationPath"
    Write-Output "MANIFEST_PATH=$manifestFilePath"
    Write-Output "SOURCE_COMMIT=$($manifest.sourceCommit)"
}
catch {
    if (Test-Path -LiteralPath $destinationPath) {
        Remove-Item -LiteralPath $destinationPath -Recurse -Force
    }
    if (Test-Path -LiteralPath $manifestFilePath) {
        Remove-Item -LiteralPath $manifestFilePath -Force
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
