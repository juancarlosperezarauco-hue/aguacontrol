# Decisión: operación sin pagos en línea

## Problema

El flujo de QR y tarjeta era demostrativo y no forma parte del alcance operativo actual de AquaControl.

## Solución aplicada

- Se retiraron de la Web los menús, formularios y vistas de QR, tarjeta, pagos y puntos de pago.
- Se deshabilitaron los endpoints de inicio y confirmación de pagos.
- Los avisos de corte se resuelven ahora mediante una acción administrativa con motivo. Si existe una orden de corte sin ejecución física, esta se cancela y se notifica al operario.
- Los reportes se enfocan en órdenes y materiales.

## Datos e impacto

No se eliminó ninguna tabla ni dato histórico de pagos. Las tablas existentes permanecen únicamente para conservar consistencia e historial de facturas ya registradas. El saldo conserva esa trazabilidad, pero no se pueden crear nuevos cobros desde AquaControl.

## Rollback

La restauración se realiza desde el respaldo SQL previo o desde el historial Git anterior a esta decisión. No se requiere una migración destructiva.
