[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectPath = Join-Path $repositoryRoot 'experiments\legacy-isolation-probes\AppContainerIsolationProbe\AppContainerIsolationProbe.csproj'
$baselineRoot = Join-Path $repositoryRoot 'benchmark\BenchmarkTarget'
$nodePath = (Get-Command node -ErrorAction Stop).Source
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$probeId = [guid]::NewGuid().ToString('N')
$publishRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot "p12-publish-$probeId"))
$runRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot "p12-run-$probeId"))

foreach ($path in @($publishRoot, $runRoot)) {
    if (-not $path.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or $path -eq $temporaryRoot) {
        throw "Percorso temporaneo non sicuro: $path"
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $baselineRoot 'node_modules'))) {
    throw 'Le dipendenze bloccate della baseline non sono presenti. P12 non esegue installazioni.'
}

$probeExitCode = 2
try {
    dotnet publish $projectPath --configuration Release --output $publishRoot --no-restore --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Pubblicazione del probe P12 non riuscita con codice $LASTEXITCODE."
    }

    $executablePath = Join-Path $publishRoot 'AppContainerIsolationProbe.exe'
    & $executablePath --node-controller $baselineRoot $nodePath $runRoot
    $probeExitCode = $LASTEXITCODE
}
finally {
    if (Test-Path -LiteralPath $publishRoot) {
        Remove-Item -LiteralPath $publishRoot -Recurse -Force
    }
}

if ($probeExitCode -ne 0) {
    throw "Il probe P12 non ha superato tutti i controlli (codice $probeExitCode)."
}
