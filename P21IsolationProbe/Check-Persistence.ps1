[CmdletBinding()]
param()

$markerPresent = Test-Path -LiteralPath 'C:\P21PersistenceMarker.txt'
[ordered]@{
    checked = $true
    previousMarkerAbsent = (-not $markerPresent)
} | ConvertTo-Json | Set-Content -Encoding utf8 'C:\P21Output\persistence-report.json'

if ($markerPresent) {
    exit 1
}

exit 0
