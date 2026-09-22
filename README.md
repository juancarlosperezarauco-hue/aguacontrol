# AquaControl

Sistema web para la gestión integral de un servicio de agua potable: abonados, conexiones, medidores, consumo, facturación, deudas, pagos simulados, cortes, órdenes de trabajo y cartografía SIG.

## Inicio rápido

La aplicación, scripts y documentación se encuentran en [AquaControl/](AquaControl/). Los datos SIG originales requeridos para la importación están en [DatosSIG_Reproj/](DatosSIG_Reproj/).

```powershell
& .\AquaControl\scripts\run.ps1 -SandboxPayments
```

Abrir `http://localhost:5080`.

Para instalar el proyecto en otro equipo o preparar una base nueva, consulte [AquaControl/GUIA_ENTREGA.md](AquaControl/GUIA_ENTREGA.md). La documentación funcional y técnica está en [AquaControl/README.md](AquaControl/README.md).

## Estructura

```text
AquaControl/       Aplicación ASP.NET Core, API, interfaz web, SQL y pruebas
DatosSIG_Reproj/   Capas SIG originales en WGS84
ScriptDatabaseV13/ Scripts de la instalación original analizada
output/analisis/   Análisis y validaciones del proyecto
```

Los pagos QR y tarjeta son simulados. No se procesan cargos reales.
