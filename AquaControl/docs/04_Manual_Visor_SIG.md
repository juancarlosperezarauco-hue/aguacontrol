# Manual breve del visor SIG

1. Inicie sesión con un perfil que tenga permiso de territorio.
2. Abra **Territorio y servicio** desde el menú.
3. Active o desactive capas y acerque el mapa a nivel 16 para ver lotes y códigos fijos.
4. Escriba al menos dos caracteres para buscar por código, lote, manzana o vía. Pulse **Ver** para centrar la entidad y leer sus atributos.
5. Use el selector superior derecho para cambiar entre Calles, Vista clara, Vista oscura, Satélite y OpenTopoMap.
6. Para mostrar lugares de interés, centre el mapa y pulse **Lugares cercanos · OpenTripMap**. El administrador debe configurar antes la clave API en el servidor.

## Configuración de OpenTripMap

En PowerShell del servidor:

```powershell
[Environment]::SetEnvironmentVariable('AquaControl__OpenTripMap__ApiKey','SU_CLAVE','User')
```

Después reinicie AquaControl. La clave no se agrega a `appsettings.json`, no se sube a Git y no se distribuye con la aplicación.
