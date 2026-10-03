# Arquitectura y diagramas

## Arquitectura de ejecución

```mermaid
flowchart LR
  U[Cliente / Supervisor / Operario] --> W[Web responsive + Leaflet]
  W -->|HTTPS / API REST| A[ASP.NET Core API]
  A --> AP[Application: reglas de negocio]
  AP --> IN[Infrastructure: EF Core, SIG, pagos simulados]
  IN --> SQL[(SQL Server + Spatial 4326)]
  A --> OSM[Overpass / OpenStreetMap: referencias cercanas]
  W --> T[Capas base: OSM, CARTO, Esri, OpenTopoMap]
```

La Web solo llama a `/api/geo/places`; AquaControl consulta el servicio de referencias desde el servidor con límites y caché.

## Modelo de clases principal

```mermaid
classDiagram
  class Client { +Id +Name +Document }
  class Account { +Id +Number +Status }
  class Connection { +Id +Code +Status +FixedCodeId }
  class Meter { +Id +Serial }
  class MeterInstallation { +ConnectionId +MeterId }
  class Reading { +Period +Value }
  class Invoice { +Number +Total +DueAt }
  class Payment { +Reference +Amount +Method }
  class WorkOrder { +Number +Status +ScheduledAt }
  class WorkActivity { +Done +Result }
  class FixedCode { +Geom +CodFijo }
  class Lot { +Geom }
  Client "1" --> "*" Account
  Account "1" --> "*" Connection
  Connection "1" --> "*" MeterInstallation
  Meter "1" --> "*" MeterInstallation
  MeterInstallation "1" --> "*" Reading
  Account "1" --> "*" Invoice
  Account "1" --> "*" Payment
  Connection "1" --> "*" WorkOrder
  WorkOrder "1" --> "*" WorkActivity
  Connection "*" --> "0..1" FixedCode
  FixedCode "*" --> "0..1" Lot
```

## Relación espacial y comercial

```mermaid
erDiagram
  MANZANAS ||--o{ LOTES : contiene
  LOTES ||--o{ CODIGOS_FIJOS : ubica
  CODIGOS_FIJOS ||--o{ CONEXIONES : referencia
  CLIENTES ||--o{ CUENTAS : titular
  CUENTAS ||--o{ CONTRATOS : mantiene
  CONEXIONES ||--o{ CONTRATOS : servicio
  CONTRATOS ||--o{ FACTURAS : factura
  CUENTAS ||--o{ PAGOS : recibe
  CONTRATOS ||--o{ AVISOS_CORTE : genera
  AVISOS_CORTE ||--o| ORDENES_TRABAJO : origina
```

## Casos de uso prioritarios

- Registrar lectura y generar factura por período.
- Registrar pago simulado y actualizar el saldo.
- Generar aviso de corte y cancelar la orden cuando se confirma el pago.
- Crear, asignar, ejecutar, verificar y cerrar una orden.
- Buscar código, lote, manzana o vía; inspeccionar atributos y ubicar la entidad en el mapa.
