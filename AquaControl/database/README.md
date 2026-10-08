# Ejecución ordenada de scripts SQL

1. Cree un respaldo con `00_Backup_Original.sql`.
2. Para una base nueva, use `01_AquaControl_Inicial.sql` o `scripts/setup.ps1 -Initialize`.
3. Para conservar usuarios y menús de una instalación anterior, revise y aplique `02_Importar_Legado.sql` sobre una copia.
4. Aplique `04_Exigir_CodigoFijo_Conexion.sql` solo después de corregir posibles conexiones duplicadas.
5. Aplique `05_Completar_Bitacora_Migrador_SIG.sql` a bases creadas antes de la ampliación del migrador SIG.
6. Ejecute `06_Verificar_PreDespliegue.sql`. Si informa elementos faltantes, no despliegue hasta resolverlos.
7. Para reemplazar el padrón demostrativo por los titulares provenientes de los Códigos Fijos SIG ya importados, ejecute `12_Poblar_Padron_Clientes_Desde_SIG.sql`. El script genera respaldos, no elimina registros y no inventa medidores ni tarifas oficiales.

Los scripts de actualización no eliminan geometrías ni clientes. No ejecute scripts de estructura sobre la base original sin respaldo verificable.
