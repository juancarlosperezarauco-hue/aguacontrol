param(
    [Parameter(Mandatory = $true)]
    [string]$Ruta
)

$ErrorActionPreference = 'Stop'

$esquemas = [ordered]@{
    '01_clientes.csv' = @('documento','nombre','telefono','email','direccion','activo')
    '02_cuentas.csv' = @('numero_cuenta','activo')
    '03_conexiones.csv' = @('codigo_conexion','codigo_fijo','id_sector','id_via','direccion','longitud','latitud','estado')
    '04_medidores.csv' = @('serie','modelo','activo')
    '05_instalaciones_medidor.csv' = @('codigo_conexion','serie','lectura_inicial','fecha_instalacion')
    '06_tarifas.csv' = @('nombre','moneda','cargo_fijo','precio_m3','fecha_inicio','activa')
    '07_contratos.csv' = @('documento','numero_cuenta','codigo_conexion','tarifa','fecha_inicio','fecha_fin')
}

if (-not (Test-Path -LiteralPath $Ruta -PathType Container)) {
    throw "No existe la carpeta de carga: $Ruta"
}

$errores = [System.Collections.Generic.List[string]]::new()
$resumen = [System.Collections.Generic.List[object]]::new()
foreach ($archivo in $esquemas.Keys) {
    $rutaArchivo = Join-Path $Ruta $archivo
    if (-not (Test-Path -LiteralPath $rutaArchivo -PathType Leaf)) {
        $errores.Add("Falta el archivo $archivo")
        continue
    }

    $encabezado = (Get-Content -LiteralPath $rutaArchivo -Encoding UTF8 -TotalCount 1).Trim([char]0xFEFF)
    $columnas = @($encabezado -split ',' | ForEach-Object { $_.Trim() })
    $faltantes = @($esquemas[$archivo] | Where-Object { $_ -notin $columnas })
    if ($faltantes.Count -gt 0) {
        $errores.Add("${archivo}: faltan columnas: $($faltantes -join ', ')")
        continue
    }

    $filas = @(Import-Csv -LiteralPath $rutaArchivo -Encoding UTF8)
    $resumen.Add([pscustomobject]@{ Archivo = $archivo; Filas = $filas.Count; Estado = 'VALIDO' })
}

$resumen | Format-Table -AutoSize
if ($errores.Count -gt 0) {
    Write-Host "`nErrores encontrados:" -ForegroundColor Red
    $errores | ForEach-Object { Write-Host "- $_" -ForegroundColor Red }
    exit 1
}

Write-Host "`nEstructura válida. No se modificó la base de datos." -ForegroundColor Green
