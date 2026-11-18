[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][string]$DestinationRoot
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$commit = (& git -C $repositoryRoot rev-parse --verify "$Commit^{commit}").Trim()
if ($LASTEXITCODE -ne 0 -or $commit -ne $Commit.ToLowerInvariant()) { throw 'Commit non valido o non completo.' }
$destination = [IO.Path]::GetFullPath($DestinationRoot)
if (Test-Path -LiteralPath $destination) { throw "La destinazione esiste già: '$destination'." }
$hostNode = (Get-Command node -ErrorAction Stop).Source
$npmCli = Join-Path (Split-Path -Parent $hostNode) 'node_modules\npm\bin\npm-cli.js'
if (-not (Test-Path -LiteralPath $npmCli -PathType Leaf)) { throw "npm-cli.js non trovato: '$npmCli'." }
$temp = Join-Path ([IO.Path]::GetTempPath()) ('frozen-toolchain-' + [guid]::NewGuid().ToString('N'))

function New-Installation([string]$root) {
    $archive = Join-Path $root 'packages.tar'; $packages = Join-Path $root 'packages'; $nodeDir = Join-Path $root 'node'
    New-Item -ItemType Directory -Path $root, $packages, $nodeDir | Out-Null
    & git -C $repositoryRoot -c core.autocrlf=false -c core.eol=lf archive --format=tar "--output=$archive" "$commit`:benchmark/BenchmarkTarget" package.json package-lock.json
    if ($LASTEXITCODE -ne 0) { throw 'Estrazione di package.json e package-lock.json non riuscita.' }
    & tar -xf $archive -C $packages
    if ($LASTEXITCODE -ne 0) { throw 'Estrazione archivio npm non riuscita.' }
    Copy-Item -LiteralPath $hostNode -Destination (Join-Path $nodeDir 'node.exe')
    Push-Location $packages
    try { & (Join-Path $nodeDir 'node.exe') $npmCli ci --no-audit --no-fund }
    finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw 'npm ci non riuscito.' }
}

function Get-Entries([string]$root) {
    $prefix = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'; $map = @{}
    foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse) {
        if ($file.Name -eq 'packages.tar') { continue }
        $path = $file.FullName.Substring($prefix.Length).Replace('\','/'); $map[$path] = $file
    }
    $paths = [string[]]$map.Keys; [Array]::Sort($paths,[StringComparer]::Ordinal)
    return @($paths | ForEach-Object { $f=$map[$_]; [pscustomobject][ordered]@{path=$_;length=$f.Length;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $f.FullName).Hash.ToLowerInvariant()} })
}

function Get-Aggregate([object[]]$entries) {
    $text = ($entries | ForEach-Object { "$($_.path)`0$($_.length)`0$($_.sha256)`n" }) -join ''
    $sha=[Security.Cryptography.SHA256]::Create(); try { return ([BitConverter]::ToString($sha.ComputeHash([Text.UTF8Encoding]::new($false).GetBytes($text)))).Replace('-','').ToLowerInvariant() } finally {$sha.Dispose()}
}

New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $first=Join-Path $temp 'first'; $second=Join-Path $temp 'second'; New-Installation $first; New-Installation $second
    $a=Get-Entries $first; $b=Get-Entries $second
    if (($a|ConvertTo-Json -Compress) -ne ($b|ConvertTo-Json -Compress)) { throw 'Le due installazioni npm non sono byte-per-byte identiche.' }
    New-Item -ItemType Directory -Path $destination | Out-Null
    Copy-Item -LiteralPath (Join-Path $first 'node') -Destination $destination -Recurse
    Copy-Item -LiteralPath (Join-Path $first 'packages') -Destination $destination -Recurse
    $aggregate=Get-Aggregate $a
    $manifest=[ordered]@{schemaVersion=1;sourceCommit=$commit;nodeVersion=(& (Join-Path $destination 'node\node.exe') --version);nodeSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $destination 'node\node.exe')).Hash.ToLowerInvariant();npmVersion=(& (Join-Path $destination 'node\node.exe') $npmCli --version);npmBootstrapPath=$npmCli;fileCount=$a.Count;aggregateSha256=$aggregate;files=$a}
    $manifestPath=Join-Path $destination 'toolchain-manifest.json'; [IO.File]::WriteAllText($manifestPath,(($manifest|ConvertTo-Json -Depth 8).Replace("`r`n","`n")+"`n"),[Text.UTF8Encoding]::new($false))
    Get-ChildItem -LiteralPath $destination -File -Recurse | ForEach-Object {$_.IsReadOnly=$true}
    Write-Output "TOOLCHAIN_ROOT=$destination"; Write-Output "FILE_COUNT=$($a.Count)"; Write-Output "AGGREGATE_SHA256=$aggregate"; Write-Output "MANIFEST_SHA256=$((Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash.ToLowerInvariant())"
}
catch {
    if (Test-Path -LiteralPath $destination) { Get-ChildItem -LiteralPath $destination -File -Recurse | ForEach-Object {$_.IsReadOnly=$false}; Remove-Item -LiteralPath $destination -Recurse -Force }
    throw
}
finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force } }
