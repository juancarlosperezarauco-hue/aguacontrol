# Carga del padrón de clientes desde SIG

La carga comercial utiliza las capas SIG originales ya importadas en `AquaControlDev`.

## Información utilizada

| Origen SIG | Uso en AquaControl |
|---|---|
| `CodigosFijos.Nombre` | Titular o cliente |
| `CodigosFijos.IdCodigo` | Identificador técnico de origen |
| `CodigosFijos.CodFijo` | Código fijo mostrado en el servicio |
| Geometría del Código Fijo | Coordenadas de la conexión |
| `Lotes` y `Manzanas` | Dirección territorial de referencia |
| `CodigosFijos.Estado` | Estado operativo de la conexión |

## Resultado actual

La fuente entregada contiene 6.271 Códigos Fijos con titular y geometría válidos. Cada uno genera:

- un cliente con documento técnico `SIG-CF-{IdCodigo}`;
- un abonado `AC-SIG-{IdCodigo}`;
- una conexión `CNX-SIG-{IdCodigo}`;
- un contrato vigente vinculado a la conexión y al titular.

La fuente no incluye series, modelo ni lecturas iniciales de medidores. AquaControl no crea equipos ficticios: el mapa muestra el servicio y permite crear una orden de instalación o registrar el medidor real cuando sea verificado en campo.

## Protección de información previa

El script [12_Poblar_Padron_Clientes_Desde_SIG.sql](../database/12_Poblar_Padron_Clientes_Desde_SIG.sql) guarda las tablas `RespaldoPadronDemoAntesSIG_*` antes de cerrar los contratos demostrativos. No elimina clientes, conexiones, medidores, facturas ni órdenes previas.

La tarifa creada para estos contratos queda inactiva y se denomina **Tarifa SIG pendiente de aprobación**. Antes de registrar lecturas y emitir facturas, el administrador debe crear y aprobar la tarifa oficial.
