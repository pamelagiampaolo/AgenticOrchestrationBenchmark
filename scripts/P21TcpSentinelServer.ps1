[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int]$Port,

    [Parameter(Mandatory)]
    [string]$LogPath,

    [Parameter(Mandatory)]
    [string]$ReadyPath
)

$ErrorActionPreference = 'Stop'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Any, $Port)
$listener.Start()
Set-Content -NoNewline -Encoding ascii $ReadyPath 'READY'

try {
    while ($true) {
        $client = $listener.AcceptTcpClient()
        try {
            $remoteAddress = $client.Client.RemoteEndPoint.Address.ToString()
            Add-Content -Encoding utf8 $LogPath "$(Get-Date -Format o)|$remoteAddress"
            $stream = $client.GetStream()
            $buffer = New-Object byte[] 64
            $stream.ReadTimeout = 1000
            try {
                $stream.Read($buffer, 0, $buffer.Length) | Out-Null
            }
            catch [IO.IOException] {
            }
        }
        finally {
            $client.Dispose()
        }
    }
}
finally {
    $listener.Stop()
}
