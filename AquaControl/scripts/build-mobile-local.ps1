param([string]$GradleCommand,[switch]$Offline)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$androidRoot=Join-Path $projectRoot 'mobile/android'
if(!$env:JAVA_HOME){throw 'Defina JAVA_HOME con un JDK 21 antes de compilar.'}
$keytool=Join-Path $env:JAVA_HOME 'bin/keytool.exe'
if(!(Test-Path -LiteralPath $keytool)){throw 'No se encontró keytool en JAVA_HOME.'}
$privateFolder=Join-Path $projectRoot '.local/signing'
New-Item -ItemType Directory -Path $privateFolder -Force | Out-Null
$settingsFile=Join-Path $privateFolder 'local-signing.json'
$keyFile=Join-Path $privateFolder 'aquacontrol-local.p12'
if(!(Test-Path -LiteralPath $settingsFile)){
 if(Test-Path -LiteralPath $keyFile){throw 'Existe una clave sin sus datos de recuperación. No se sobrescribió.'}
 $random=New-Object byte[] 36;$rng=[Security.Cryptography.RandomNumberGenerator]::Create();try{$rng.GetBytes($random)}finally{$rng.Dispose()}
 @{alias='aquacontrol-local';password=[Convert]::ToBase64String($random)} | ConvertTo-Json | Set-Content -LiteralPath $settingsFile -Encoding UTF8
}
$settings=Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
$names=@('AQUA_ANDROID_KEYSTORE','AQUA_ANDROID_STORE_PASSWORD','AQUA_ANDROID_KEY_ALIAS');$previous=@{}
foreach($name in $names){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
 $env:AQUA_ANDROID_KEYSTORE=$keyFile;$env:AQUA_ANDROID_STORE_PASSWORD=$settings.password;$env:AQUA_ANDROID_KEY_ALIAS=$settings.alias
 if(!(Test-Path -LiteralPath $keyFile)){
  & $keytool -genkeypair -keystore $keyFile -storetype PKCS12 -alias $settings.alias -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=AquaControl Local,OU=Equipo AquaControl,O=AquaControl' -storepass:env AQUA_ANDROID_STORE_PASSWORD -keypass:env AQUA_ANDROID_STORE_PASSWORD -noprompt
  if($LASTEXITCODE -ne 0){throw 'No se pudo generar la clave de firma local.'}
 }
 if(!$GradleCommand){$GradleCommand=Join-Path $androidRoot 'gradlew.bat'}
 $gradleArguments=@('-p',$androidRoot,'assembleLocal','assembleDebug','--no-daemon','--console=plain');if($Offline){$gradleArguments+='--offline'}
 & $GradleCommand @gradleArguments
 if($LASTEXITCODE -ne 0){throw 'Falló la compilación Android.'}
 $sdk=if($env:ANDROID_HOME){$env:ANDROID_HOME}else{Join-Path $env:LOCALAPPDATA 'Android/Sdk'}
 $signer=Join-Path $sdk 'build-tools/35.0.0/apksigner.bat'
 if(!(Test-Path -LiteralPath $signer)){throw 'Instale Android Build Tools 35.0.0 para verificar la firma.'}
 $apk=Join-Path $androidRoot 'app/build/outputs/apk/local/app-local.apk'
 & $signer verify --verbose $apk
 if($LASTEXITCODE -ne 0){throw 'La APK local no tiene una firma válida.'}
 $output=Join-Path $projectRoot 'output/mobile';New-Item -ItemType Directory -Path $output -Force | Out-Null
 Copy-Item -LiteralPath $apk -Destination (Join-Path $output 'AquaControl-Local-v0.4.apk')
 Copy-Item -LiteralPath (Join-Path $androidRoot 'app/build/outputs/apk/debug/app-debug.apk') -Destination (Join-Path $output 'AquaControl-Campo-v0.4-debug.apk')
 Get-FileHash -LiteralPath (Join-Path $output 'AquaControl-Local-v0.4.apk') -Algorithm SHA256 | Select-Object Hash,Path
 Write-Host 'Guarde una copia privada de .local/signing. No se incluye en Git ni en el paquete para instalar.'
}finally{foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}}
