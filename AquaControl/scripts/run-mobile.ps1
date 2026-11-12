param([int]$Port=5080)
# Inicio explícito para teléfonos de la misma red; conserva el acceso por localhost.
& (Join-Path $PSScriptRoot 'run.ps1') -ListenHost lan -Port $Port
