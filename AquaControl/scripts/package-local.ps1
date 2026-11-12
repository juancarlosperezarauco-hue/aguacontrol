param([switch]$SkipMobile)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent;$workspace=Split-Path $projectRoot -Parent
$stamp=Get-Date -Format 'yyyyMMdd_HHmmss';$output=Join-Path $projectRoot 'output';$folder=Join-Path $output "AquaControl-Local-$stamp"
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$files=@(& git -C $workspace ls-files --cached --others --exclude-standard)
if($LASTEXITCODE -ne 0){throw 'No se pudo obtener el inventario de código.'}
foreach($file in $files){
 if($file -match '^AquaControl/database/backups/' -or $file -match '(?:^|/)(?:\.local|App_Data|bin|obj|\.gradle|\.kotlin|build)(?:/|$)' -or $file -match '\.(?:bak|p12|pfx|jks|keystore|apk)$'){continue}
 if($file.StartsWith('AquaControl/')){$relative=$file.Substring('AquaControl/'.Length)}elseif($file.StartsWith('DatosSIG_Reproj/')){$relative=$file}else{continue}
 $source=Join-Path $workspace $file;if(!(Test-Path -LiteralPath $source -PathType Leaf)){continue}
 $target=Join-Path $folder $relative;New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null;Copy-Item -LiteralPath $source -Destination $target
}
$ignore=Get-Content -LiteralPath (Join-Path $workspace '.gitignore') | Where-Object {$_ -notmatch '^!AquaControl/database/backups/'} | ForEach-Object {$_.Replace('AquaControl/','')}
@($ignore)+@('server/') | Set-Content -LiteralPath (Join-Path $folder '.gitignore') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $workspace '.gitattributes') -Destination (Join-Path $folder '.gitattributes')
& dotnet publish (Join-Path $projectRoot 'src/AquaControl.API') -c Release -o (Join-Path $folder 'server') --no-restore -v quiet
if($LASTEXITCODE -ne 0){throw 'Falló la publicación del backend.'}
if(!$SkipMobile){
 $apk=Join-Path $output 'mobile/AquaControl-Local-v0.4.apk';if(!(Test-Path -LiteralPath $apk)){throw 'Compile primero scripts/build-mobile-local.ps1 o use -SkipMobile.'}
 $mobile=Join-Path $folder 'instalar-android';New-Item -ItemType Directory -Path $mobile -Force | Out-Null;Copy-Item -LiteralPath $apk -Destination $mobile
}
$launcher='@echo off'+"`r`n"+'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start-local.ps1"'+"`r`n"+'pause'+"`r`n"
Set-Content -LiteralPath (Join-Path $folder 'INICIAR_AQUACONTROL.cmd') -Value $launcher -Encoding ASCII
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/14_Entrega_Local.md') -Destination (Join-Path $folder 'LEEME_PRIMERO.md')
$manifest=@{createdAt=[DateTime]::UtcNow.ToString('O');commit=(& git -C $workspace rev-parse HEAD);databaseBackupIncluded=$false;privateKeysIncluded=$false;files=@(Get-ChildItem -LiteralPath $folder -Recurse -File | ForEach-Object {@{path=$_.FullName.Substring($folder.Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $folder 'manifest.json') -Encoding UTF8
$zip=$folder+'.zip';Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $folder -Force | Select-Object -ExpandProperty FullName) -DestinationPath $zip -CompressionLevel Optimal
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Select-Object Hash,Path
Write-Host 'Paquete local generado. El respaldo SQL y las fotos existentes se transfieren por separado.'
