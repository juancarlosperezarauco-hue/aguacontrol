param(
    [int]$Port=5080,
    [Alias('Host')]
    [string]$ListenHost
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if (!$ListenHost) {
    $hostSetting=Join-Path $projectRoot '.local/listen-host.txt'
    $ListenHost=if(Test-Path -LiteralPath $hostSetting){(Get-Content -LiteralPath $hostSetting -Raw).Trim()}else{'localhost'}
}
if ($ListenHost -notmatch '^[a-zA-Z0-9.:-]+$') { throw 'Host no válido. Use localhost, lan o una dirección IP.' }
$workspace=Split-Path $projectRoot -Parent
$sdk=Join-Path $workspace '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk=(Get-Command dotnet).Source }
$env:ASPNETCORE_ENVIRONMENT='Development'
if (!$env:ConnectionStrings__Aqua) { $env:ConnectionStrings__Aqua='Server=np:\\.\pipe\sql\query;Database=AquaControlDev;Integrated Security=true;Encrypt=false;TrustServerCertificate=true' }
$listenHost = if ($ListenHost -eq 'lan') { '0.0.0.0' } else { $ListenHost }
if ($listenHost -ne 'localhost' -and $listenHost -ne '127.0.0.1') {
    $lanAddresses=@([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object OperationalStatus -eq Up | ForEach-Object { $_.GetIPProperties().UnicastAddresses } | Where-Object { $_.Address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork } | ForEach-Object { $_.Address.ToString() })
    if (!$env:AllowedHosts) { $env:AllowedHosts=(@('localhost','127.0.0.1','10.0.2.2',$env:COMPUTERNAME,$listenHost)+$lanAddresses | Select-Object -Unique) -join ';' }
    Write-Host "AquaControl admite acceso desde la red local en el puerto $Port."
    $preferredAddresses=@([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object { $_.OperationalStatus -eq 'Up' -and @($_.GetIPProperties().GatewayAddresses | Where-Object { $_.Address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork }).Count -gt 0 } | ForEach-Object { $_.GetIPProperties().UnicastAddresses } | Where-Object { $_.Address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork } | ForEach-Object { $_.Address.ToString() })
    if (!$preferredAddresses.Count) { $preferredAddresses=$lanAddresses }
    foreach ($address in $preferredAddresses) { Write-Host "Servidor para la APK (misma red): http://${address}:$Port" }
}
& $sdk run --no-build --project (Join-Path $projectRoot 'src/AquaControl.API') --urls "http://${listenHost}:$Port"
