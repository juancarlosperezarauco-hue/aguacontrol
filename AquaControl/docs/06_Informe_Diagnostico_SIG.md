# Informe de diagnóstico SIG

**Fecha:** 04-10-2026  
**Sistema:** AquaControl  
**Sistema de referencia esperado:** WGS 84 / EPSG:4326

## Fuente inspeccionada

| Capa original | Tipo geométrico esperado | Registros disponibles |
|---|---|---:|
| `Exp_CodigoFijo_4326` | Punto | 6.271 códigos fijos |
| `Exp_MapaBase_LOTES_4326` | Polígono | 15.280 lotes |
| `Exp_MapaBase_MZA_4326` | Polígono | 863 manzanas |
| `Exp_MapaBase_VIAS_4326` | Línea | 578 vías |

## Resultado técnico

Las cuatro capas se consumen como geometrías espaciales de SQL Server con SRID 4326. El migrador exige los componentes `.shp`, `.shx`, `.dbf` y `.prj`, valida la fuente antes de escribir datos y conserva atributos no mapeados en `OriginalJson`.

Se verifica que los datos puedan consultarse por extensión, retornarse como GeoJSON y localizarse por texto. La prueba automatizada de la API registra su evidencia en `AquaControl/output/sig-api-report.json`.

## Relación y calidad de datos

- Código Fijo se relaciona con Lote cuando existe una intersección espacial única.
- Lote se relaciona con Manzana usando su punto interior.
- Conexión comercial requiere un Código Fijo SIG válido y único.
- Los elementos sin vínculo no se descartan: se muestran para revisión catastral en el visor.

## Evidencia generada

Ejecutar:

```powershell
& .\AquaControl\scripts\validate-sig.ps1 -Report .\AquaControl\output\analisis\Validacion_SIG_20261004.json
python .\AquaControl\tests\sig_api.py http://localhost:5080 --password-file .\AquaControl\.local\acceso-inicial.txt --report .\AquaControl\output\sig-api-report.json
```

La validación es de solo lectura y no altera las capas ni los datos comerciales.
