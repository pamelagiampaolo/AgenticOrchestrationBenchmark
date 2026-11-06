[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-Content -NoNewline -Encoding utf8 'C:\P21PersistenceMarker.txt' 'EPHEMERAL_MARKER'
Set-Content -NoNewline -Encoding utf8 'C:\P21Output\timeout-started.txt' 'TIMEOUT_PROBE_STARTED'
Start-Sleep -Seconds 300
