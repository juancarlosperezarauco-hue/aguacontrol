# Verificación de la entrega local 0.4

Fecha: 12 de noviembre de 2026. Destino acordado: equipo local Windows y teléfonos en la misma red.

| Comprobación | Resultado |
|---|---|
| Compilación backend .NET 10 | Correcta, sin advertencias ni errores en la compilación final |
| Pruebas de roles, permisos, GPS, visitas y evidencias | 66 aprobadas en AquaControlRoleTests |
| Pruebas de grafo y Dijkstra | 10 aprobadas; incluye lectura de la capa Vías real sin modificarla |
| Asignación directa y operación completa | 22 aprobadas en AquaControlDispatchTests |
| Reintentos de asignación | Misma solicitud devuelve la misma ruta sin duplicar órdenes |
| Validación concurrente | Una modificación después de la vista previa exige recalcular y no deja órdenes parciales |
| Flujo de consumo | Lectura anterior 5, lectura actual 10, consumo 5 y recibo 20 en tarifa ficticia de prueba |
| Corte / reconexión | Aviso, autorización, actividades, foto, GPS, lectura, efecto físico, verificación y cierre comprobados mediante servicios sobre SQL real de pruebas |
| Modelos JavaScript | Reproducción, pausa, reinicio, avance, velocidad, salida y límite/georreferencia de teselas aprobados |
| Navegador | Asignación desde servicios guardada en la BD de pruebas; cambio entre ambos modos de planificación; reproducción hasta la última posición |
| Visor 3D | Carga de 58 manzanas, 62 vías, 6 servicios ficticios y 6/6 teselas; cambio a satélite y vista superior sin errores de consola |
| Responsive | Anchos de 360, 768 y 1366 comprobados por dimensiones DOM; sin desbordamiento horizontal de página en las pantallas nuevas |
| Auditoría NuGet | No se reportan paquetes vulnerables en los orígenes consultados tras actualizar Pkcs a 9.0.21 |
| Android | assembleLocal y assembleDebug correctos; APK local firmada, verificación v2 válida, versión 0.4.0, código 4 |
| Arranque local | BD y esquema verificados; proceso iniciado y HTTP de autenticación disponible |
| Datos existentes | Incremento 17 aditivo y respaldo SQL previo con CHECKSUM y VERIFYONLY |

Total de comprobaciones C#: **98**. Las pruebas usan usuarios y registros FICTICIOS en bases separadas; los servicios operativos no se poblaron con clientes o medidores de demostración nuevos.

## Repetir

```powershell
dotnet run --project tests/Roles
dotnet run --project tests/Scope -- --real-network
dotnet run --project tests/Dispatch
node tests/browser-models.cjs
dotnet list src/AquaControl.API package --vulnerable --include-transitive
```

Scope sin `--real-network` prueba el algoritmo sin SQL y se ejecuta también en CI. Los otros dos proyectos restringen explícitamente la base de destino de pruebas. La instalación del modelo completo se comprobó en una BD nueva de pruebas; no se afirma que se haya instalado físicamente en otra computadora.

## Límites de aceptación

No hay teléfono conectado por ADB. Falta comprobar físicamente GPS/segundo plano, cámara, red Wi-Fi y navegación en el teléfono del usuario. Las muestras GPS de las pruebas automatizadas son ficticias y están aisladas de la BD operativa. No se realizó publicación en Internet; el usuario eligió uso local. OCR sigue siendo opcional y no se incorpora en esta entrega.
