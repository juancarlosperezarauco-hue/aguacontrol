param([ValidateRange(1024,65535)][int]$Port=5080,[string]$SqlServer='np:\\.\pipe\sql\query',[string]$Database='AquaControlDev',[switch]$Restart,[switch]$NoBuild)
$ErrorActionPreference='Stop'
if($Database -notmatch '^AquaControl[A-Za-z0-9_]*$'){throw 'Seleccione una base AquaControl; nunca la base SIG original.'}
$projectRoot=Split-Path $PSScriptRoot -Parent
$dotnet=(Get-Command dotnet -ErrorAction Stop).Source
$project=Join-Path $projectRoot 'src/AquaControl.API/AquaControl.API.csproj'
$packaged=Test-Path -LiteralPath (Join-Path $projectRoot 'server/AquaControl.API.exe')
$api=if($packaged){Join-Path $projectRoot 'server/AquaControl.API.exe'}else{Join-Path $projectRoot 'src/AquaControl.API/bin/Debug/net10.0/AquaControl.API.exe'}
$api=[IO.Path]::GetFullPath($api)
$own=@(Get-CimInstance Win32_Process -Filter "Name='AquaControl.API.exe'" | Where-Object { $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -eq $api })
if($own.Count){
 if(!$Restart){Write-Host 'Ya existe una instancia de este proyecto. Use -Restart para reiniciarla con la versión actual.';return}
 foreach($process in $own){Stop-Process -Id $process.ProcessId -Force}
}
if(!$packaged -and !$NoBuild){& $dotnet build $project -v quiet;if($LASTEXITCODE -ne 0){throw 'Falló la compilación.'}}
if(!(Test-Path -LiteralPath $api)){throw 'No se encontró el ejecutable. Compile el proyecto o vuelva a generar el paquete.'}
$builder=New-Object System.Data.Common.DbConnectionStringBuilder
$builder['Server']=$SqlServer;$builder['Database']=$Database;$builder['Integrated Security']=$true;$builder['Encrypt']=$false;$builder['TrustServerCertificate']=$true
if(!$env:ConnectionStrings__Aqua){$env:ConnectionStrings__Aqua=$builder.ConnectionString}
$env:ASPNETCORE_ENVIRONMENT='Development'
$properties=@([Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object OperationalStatus -eq Up | ForEach-Object { $_.GetIPProperties() })
$addresses=@($properties | ForEach-Object { $_.UnicastAddresses } | Where-Object { $_.Address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork } | ForEach-Object { $_.Address.ToString() })
$env:AllowedHosts=(@('localhost','127.0.0.1','10.0.2.2',$env:COMPUTERNAME)+$addresses | Select-Object -Unique) -join ';'
if(!$env:Storage__EvidencePath){$env:Storage__EvidencePath=if($packaged){Join-Path $projectRoot '.local/evidence'}else{Join-Path $projectRoot 'src/AquaControl.API/App_Data/evidence'}}
if(!$env:Sig__SourcePath){$sig=Join-Path $projectRoot 'DatosSIG_Reproj';if(!(Test-Path -LiteralPath $sig)){$sig=Join-Path (Split-Path $projectRoot -Parent) 'DatosSIG_Reproj'};if(Test-Path -LiteralPath $sig){$env:Sig__SourcePath=$sig}}
$working=if($packaged){Join-Path $projectRoot 'server'}else{Split-Path $project -Parent}
& $api --verify
if($LASTEXITCODE -ne 0){throw 'No se pudo conectar a la BD. Restaure un respaldo o inicialice una base nueva según la guía.'}
$logs=Join-Path $projectRoot '.local/logs';New-Item -ItemType Directory -Path $logs -Force | Out-Null
$process=Start-Process -FilePath $api -ArgumentList @('--urls',"http://0.0.0.0:$Port") -WorkingDirectory $working -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logs 'server.log') -RedirectStandardError (Join-Path $logs 'server-error.log') -PassThru
$ready=$false
for($attempt=0;$attempt -lt 30;$attempt++){
 if($process.HasExited){throw 'El servidor finalizó. Revise .local/logs/server-error.log (posible puerto ocupado).'}
 try{$response=Invoke-WebRequest "http://127.0.0.1:$Port/api/auth/csrf" -UseBasicParsing -TimeoutSec 5;if($response.StatusCode -eq 200){$ready=$true;break}}catch{}
 Start-Sleep -Milliseconds 500
}
if(!$ready){throw 'El servidor no respondió. Revise los registros locales.'}
Write-Host "AquaControl iniciado (PID $($process.Id)): http://localhost:$Port"
$lan=@($properties | Where-Object { @($_.GatewayAddresses | Where-Object { $_.Address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork }).Count -gt 0 } | ForEach-Object {$_.UnicastAddresses} | Where-Object { $_.Address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork } | ForEach-Object {$_.Address.ToString()})
foreach($address in $lan){Write-Host "Dirección para AquaControl Local en el teléfono: http://${address}:$Port"}
