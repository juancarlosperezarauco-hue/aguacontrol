# Arquitectura y diagramas

## Arquitectura de ejecución

```mermaid
flowchart LR
  U[Cliente / Supervisor / Operario] --> W[Web responsive + Leaflet + modelo 3D]
  W -->|HTTPS / API REST| A[ASP.NET Core API]
  A --> AP[Application: reglas de negocio]
  AP --> IN[Infrastructure: EF Core y SIG]
  IN --> SQL[(SQL Server + Spatial 4326)]
  A --> OSM[Overpass / OpenStreetMap: referencias cercanas]
  W --> T[Capas base Esri: calles, clara, oscura, satélite y topográfica]
  W --> D[Three.js: modelo territorial 3D]
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
  class WorkOrder { +Number +Status +ScheduledAt }
  class WorkActivity { +Done +Result }
  class FixedCode { +Geom +CodFijo }
  class Lot { +Geom }
  Client "1" --> "*" Contract
  Account "1" --> "*" Contract
  Connection "1" --> "*" Contract
  Connection "1" --> "*" MeterInstallation
  Meter "1" --> "*" MeterInstallation
  MeterInstallation "1" --> "*" Reading
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
  CLIENTES ||--o{ CONTRATOS : suscribe
  CUENTAS ||--o{ CONTRATOS : mantiene
  CONEXIONES ||--o{ CONTRATOS : servicio
  CONTRATOS ||--o{ FACTURAS : factura
  CONTRATOS ||--o{ AVISOS_CORTE : genera
  AVISOS_CORTE ||--o| ORDENES_TRABAJO : origina
```

## Casos de uso prioritarios

Los flujos, actores, reglas y pantallas asociadas se detallan en `docs/07_Casos_Uso_y_Prototipos.md`.

## Estrategia Git

- `main` contiene versiones integradas y verificadas localmente.
- Cada cambio funcional se registra en un commit descriptivo y se publica en `origin/main`.
- Los cambios de base de datos se acompañan con un script SQL idempotente y una verificación de predespliegue.
- Los respaldos y archivos locales de contraseñas están excluidos del control de versiones.
