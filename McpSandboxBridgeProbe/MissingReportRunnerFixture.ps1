[CmdletBinding()]
param()

Write-Output "HOST_EVIDENCE=$(Join-Path $PSScriptRoot 'missing-evidence')"
