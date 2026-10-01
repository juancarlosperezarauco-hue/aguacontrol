# Plan de pruebas SIG

## Ejecución automatizada

Con AquaControl iniciado y un archivo local de acceso inicial, ejecutar:

```powershell
python AquaControl/tests/sig_api.py http://localhost:5080 --password-file AquaControl/.local/acceso-inicial.txt --report AquaControl/output/sig-api-report.json
```

La prueba no modifica clientes, cobros ni capas. Valida autenticación, las cuatro capas, extensión, GeoJSON, búsqueda, ficha de entidad y manejo de parámetros inválidos.

## Casos de aceptación manual

| Id | Acción | Resultado esperado |
|---|---|---|
| SIG-01 | Abrir **Territorio y servicio** | Se muestran las capas activas y la leyenda. |
| SIG-02 | Buscar un código fijo, lote, manzana o vía | La lista identifica la capa y **Ver** centra y resalta la geometría. |
| SIG-03 | Hacer clic en una geometría | Se ven atributos del sistema y atributos originales. |
| SIG-04 | Cambiar mapa base | El control Leaflet permite Calles, clara, oscura, satélite u OpenTopoMap. |
| SIG-05 | Mover el cursor | Se muestran coordenadas WGS84. |
| SIG-06 | Ejecutar una carga SIG | Se previsualizan cuatro capas, se registra avance y se conserva bitácora. |
| SIG-07 | Usar una clave OpenTripMap válida | El botón muestra referencias del entorno cerca del centro. |
| SIG-08 | Usar OpenTripMap sin clave | Se informa que falta configurar la clave, sin exponer secretos. |

## Evidencia a guardar

Guardar el JSON de la prueba, el informe de validación del migrador, una captura del mapa con capas y una captura de la ficha de una entidad. No incluir contraseñas ni claves API en las evidencias.
