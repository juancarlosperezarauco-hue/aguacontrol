# Colaborar en AquaControl

1. Cree una rama desde `main` con un nombre descriptivo: `feature/facturacion` o `fix/mapa-sig`.
2. Mantenga los cambios dentro del módulo adecuado: `Domain`, `Application`, `Infrastructure`, `API` o `Web`.
3. No incluya credenciales, archivos `.local`, bases `.bak`, carpetas `bin`, `obj` ni evidencias de usuarios reales.
4. No edite Shapefiles originales. Importe una nueva versión mediante el proceso documentado y registre la conciliación.
5. Ejecute antes de proponer cambios:

```powershell
& .\.tools\dotnet\dotnet.exe build .\AquaControl\src\AquaControl.API --no-restore
node --check .\AquaControl\Web\app.js
```

6. Para cambios de negocio, agregue o ajuste una prueba en `AquaControl/tests/` cuando corresponda.
7. Describa el impacto en datos, seguridad y SIG en la solicitud de cambio.

Los pagos siguen siendo simulados. No agregue claves bancarias, números de tarjeta o datos personales reales al repositorio.
