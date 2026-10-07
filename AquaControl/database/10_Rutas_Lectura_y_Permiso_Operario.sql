/*
   AquaControl — trabajo de lectura y permiso para captura en campo.
   Incremental: agrega solamente el tipo de trabajo y el permiso faltante.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @TipoLectura int=(SELECT Id FROM dbo.TiposTrabajo WHERE Code=N'LECTURA');
IF @TipoLectura IS NULL
BEGIN
    INSERT dbo.TiposTrabajo(Code,Name,Effect,Active)
    VALUES(N'LECTURA',N'Lectura de medidor',N'NINGUNO',1);
    SET @TipoLectura=CONVERT(int,SCOPE_IDENTITY());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ActividadesTrabajo WHERE WorkTypeId=@TipoLectura AND Sort=1)
    INSERT dbo.ActividadesTrabajo(WorkTypeId,Sort,Name,Required,EvidenceRequired)
    VALUES(@TipoLectura,1,N'Verificar medidor y conexión',1,0);

IF NOT EXISTS (SELECT 1 FROM dbo.ActividadesTrabajo WHERE WorkTypeId=@TipoLectura AND Sort=2)
    INSERT dbo.ActividadesTrabajo(WorkTypeId,Sort,Name,Required,EvidenceRequired)
    VALUES(@TipoLectura,2,N'Registrar lectura de consumo',1,0);

DECLARE @RolOperario int=(SELECT IdRol FROM dbo.Roles WHERE NombreRol=N'OPERARIO');
DECLARE @PermisoLectura int=(SELECT Id FROM dbo.Permisos WHERE Code=N'readings.write');
IF @RolOperario IS NULL THROW 51030, 'No existe el rol OPERARIO.', 1;
IF @PermisoLectura IS NULL THROW 51031, 'No existe el permiso readings.write.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.RolesPermisos WHERE RoleId=@RolOperario AND PermissionId=@PermisoLectura)
    INSERT dbo.RolesPermisos(RoleId,PermissionId) VALUES(@RolOperario,@PermisoLectura);

COMMIT TRANSACTION;

