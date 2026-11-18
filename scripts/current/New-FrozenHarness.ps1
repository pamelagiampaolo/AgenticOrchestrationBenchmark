[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{40}$|^[0-9a-fA-F]{64}$')]
    [string]$Commit,

    [Parameter(Mandatory)]
    [string]$DestinationRoot
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$resolvedCommit = (& git -C $repositoryRoot rev-parse --verify "$Commit^{commit}").Trim()
if ($LASTEXITCODE -ne 0 -or $resolvedCommit -ne $Commit.ToLowerInvariant()) {
    throw "Il commit richiesto non risolve esattamente a '$Commit'."
}

$sourceTree = (& git -C $repositoryRoot rev-parse "$resolvedCommit^{tree}").Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Impossibile risolvere il tree del commit '$resolvedCommit'."
}

$destination = [IO.Path]::GetFullPath($DestinationRoot)
if (Test-Path -LiteralPath $destination) {
    throw "La destinazione esiste già: '$destination'."
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('frozen-harness-' + [guid]::NewGuid().ToString('N'))
$expectedTemporaryPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\frozen-harness-'
$temporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
if (-not $temporaryRoot.StartsWith($expectedTemporaryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Root temporanea inattesa: '$temporaryRoot'."
}

$projects = [ordered]@{
    AgenticExperimentRunner = 'src/AgenticExperimentRunner/AgenticExperimentRunner.csproj'
    AgentWorkspaceMcpServer = 'src/AgentWorkspaceMcpServer/AgentWorkspaceMcpServer.csproj'
}
$publishProperties = @(
    '-p:ContinuousIntegrationBuild=true',
    '-p:Deterministic=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

function Expand-FrozenSource {
    param([string]$TargetRoot)

    $archivePath = Join-Path $TargetRoot 'source.tar'
    $sourceRoot = Join-Path $TargetRoot 'source'
    New-Item -ItemType Directory -Path $TargetRoot, $sourceRoot | Out-Null
    & git -C $repositoryRoot -c core.autocrlf=false -c core.eol=lf archive --format=tar "--output=$archivePath" $resolvedCommit
    if ($LASTEXITCODE -ne 0) {
        throw "Creazione dell'archivio Git non riuscita per '$resolvedCommit'."
    }
    & tar -xf $archivePath -C $sourceRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Estrazione dell'archivio Git non riuscita in '$sourceRoot'."
    }
    return $sourceRoot
}

function Publish-Harness {
    param([string]$SourceRoot, [string]$OutputRoot)

    $buildProperties = $publishProperties + @("-p:PathMap=$SourceRoot=/_/")
    foreach ($project in $projects.GetEnumerator()) {
        $projectPath = Join-Path $SourceRoot $project.Value.Replace('/', '\')
        $projectOutput = Join-Path $OutputRoot $project.Key
        & dotnet restore $projectPath --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Restore non riuscito per '$($project.Key)'."
        }
        & dotnet publish $projectPath --configuration Release --output $projectOutput --no-restore --nologo --self-contained false @buildProperties
        if ($LASTEXITCODE -ne 0) {
            throw "Publish non riuscito per '$($project.Key)'."
        }
    }
}

function Get-PublishedEntries {
    param([string]$Root)

    $rootPrefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $filesByPath = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Root -File -Recurse) {
        $relativePath = [IO.Path]::GetFullPath($file.FullName).Substring($rootPrefix.Length).Replace('\', '/')
        $filesByPath[$relativePath] = $file
    }
    $paths = [string[]]$filesByPath.Keys
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $entries = foreach ($relativePath in $paths) {
        $file = $filesByPath[$relativePath]
        [pscustomobject][ordered]@{
            path = $relativePath
            length = $file.Length
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
        }
    }
    return @($entries)
}

function Get-AggregateSha256 {
    param([object[]]$Entries)

    $canonical = ($Entries | ForEach-Object { "$($_.path)`0$($_.length)`0$($_.sha256)`n" }) -join ''
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($canonical)))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
try {
    $firstRoot = Join-Path $temporaryRoot 'first'
    $secondRoot = Join-Path $temporaryRoot 'second'
    $firstSource = Expand-FrozenSource $firstRoot
    $secondSource = Expand-FrozenSource $secondRoot
    $firstPublish = Join-Path $firstRoot 'publish'
    $secondPublish = Join-Path $secondRoot 'publish'
    Publish-Harness $firstSource $firstPublish
    Publish-Harness $secondSource $secondPublish

    $firstEntries = Get-PublishedEntries $firstPublish
    $secondEntries = Get-PublishedEntries $secondPublish
    $firstCanonical = $firstEntries | ConvertTo-Json -Depth 5 -Compress
    $secondCanonical = $secondEntries | ConvertTo-Json -Depth 5 -Compress
    if ($firstCanonical -ne $secondCanonical) {
        $firstByPath = @{}; $firstEntries | ForEach-Object { $firstByPath[$_.path] = $_ }
        $secondByPath = @{}; $secondEntries | ForEach-Object { $secondByPath[$_.path] = $_ }
        $allPaths = [string[]]@($firstByPath.Keys + $secondByPath.Keys | Select-Object -Unique)
        [Array]::Sort($allPaths, [StringComparer]::Ordinal)
        $differences = foreach ($path in $allPaths) {
            $first = $firstByPath[$path]
            $second = $secondByPath[$path]
            if ($null -eq $first -or $null -eq $second -or $first.length -ne $second.length -or $first.sha256 -ne $second.sha256) {
                "$path (prima=$($first.sha256), seconda=$($second.sha256))"
            }
        }
        throw "Le due pubblicazioni indipendenti non sono byte-per-byte identiche: $($differences -join '; ')."
    }

    $aggregate = Get-AggregateSha256 $firstEntries
    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($directory in Get-ChildItem -LiteralPath $firstPublish -Directory) {
        Copy-Item -LiteralPath $directory.FullName -Destination $destination -Recurse
    }

    $dotnetInfo = (& dotnet --info | Out-String).Trim()
    $manifest = [ordered]@{
        schemaVersion = 1
        sourceCommit = $resolvedCommit
        sourceTree = $sourceTree
        configuration = 'Release'
        selfContained = $false
        debugSymbols = $false
        reproducibilityCheck = 'two independent source extractions and publishes; file paths, lengths and SHA-256 identical'
        aggregateAlgorithm = 'sha256(path NUL length NUL sha256 LF), paths ordered ordinally'
        aggregateSha256 = $aggregate
        fileCount = $firstEntries.Count
        projects = $projects
        publishProperties = $publishProperties
        pathMap = '<temporary-source-root>=/_/'
        dotnetInfo = $dotnetInfo
        files = $firstEntries
    }
    $manifestPath = Join-Path $destination 'harness-manifest.json'
    $json = ($manifest | ConvertTo-Json -Depth 10).Replace("`r`n", "`n") + "`n"
    [IO.File]::WriteAllText($manifestPath, $json, [Text.UTF8Encoding]::new($false))

    Get-ChildItem -LiteralPath $destination -File -Recurse | ForEach-Object { $_.IsReadOnly = $true }
    Write-Output "HARNESS_ROOT=$destination"
    Write-Output "FILE_COUNT=$($firstEntries.Count)"
    Write-Output "AGGREGATE_SHA256=$aggregate"
    Write-Output "MANIFEST_SHA256=$((Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash.ToLowerInvariant())"
}
catch {
    if (Test-Path -LiteralPath $destination) {
        $resolvedDestination = [IO.Path]::GetFullPath($destination)
        $approvedParent = [IO.Path]::GetFullPath((Split-Path -Parent $destination)).TrimEnd('\') + '\'
        if (-not $resolvedDestination.StartsWith($approvedParent, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Pulizia rifiutata per destinazione inattesa '$resolvedDestination'. Errore originale: $($_.Exception.Message)"
        }
        Get-ChildItem -LiteralPath $destination -File -Recurse | ForEach-Object { $_.IsReadOnly = $false }
        Remove-Item -LiteralPath $destination -Recurse -Force
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
