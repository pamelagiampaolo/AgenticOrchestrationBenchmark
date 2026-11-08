[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'AppContainerIsolationProbe\AppContainerIsolationProbe.csproj'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$probeId = [guid]::NewGuid().ToString('N')
$publishRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot "p11-publish-$probeId"))
$runRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot "p11-run-$probeId"))

foreach ($path in @($publishRoot, $runRoot)) {
    if (-not $path.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or $path -eq $temporaryRoot) {
        throw "Percorso temporaneo non sicuro: $path"
    }
}

$probeExitCode = 2
try {
    dotnet publish $projectPath --configuration Release --output $publishRoot --no-restore --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Pubblicazione del probe P11 non riuscita con codice $LASTEXITCODE."
    }

    $executablePath = Join-Path $publishRoot 'AppContainerIsolationProbe.exe'
    & $executablePath $runRoot
    $probeExitCode = $LASTEXITCODE
}
finally {
    if (Test-Path -LiteralPath $publishRoot) {
        Remove-Item -LiteralPath $publishRoot -Recurse -Force
    }
}

if ($probeExitCode -ne 0) {
    throw "Il probe P11 non ha superato tutti i controlli (codice $probeExitCode)."
}
