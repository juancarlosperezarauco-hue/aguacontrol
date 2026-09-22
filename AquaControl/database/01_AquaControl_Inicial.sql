CREATE TABLE [Clientes] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(150) NOT NULL,
    [Document] nvarchar(40) NOT NULL,
    [Email] nvarchar(100) NOT NULL,
    [Phone] nvarchar(40) NOT NULL,
    [Address] nvarchar(250) NOT NULL,
    [Active] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Clientes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [CuentasServicio] (
    [Id] int NOT NULL IDENTITY,
    [Number] nvarchar(40) NOT NULL,
    [Active] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_CuentasServicio] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ImportacionesSIG] (
    [Id] int NOT NULL IDENTITY,
    [Layer] nvarchar(80) NOT NULL,
    [Hash] nvarchar(64) NOT NULL,
    [Count] int NOT NULL,
    [Rejected] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_ImportacionesSIG] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Materiales] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(40) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Unit] nvarchar(20) NOT NULL,
    [Cost] decimal(18,4) NOT NULL,
    [Active] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Materiales] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Medidores] (
    [Id] int NOT NULL IDENTITY,
    [Serial] nvarchar(50) NOT NULL,
    [Model] nvarchar(100) NOT NULL,
    [Active] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Medidores] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MenuOpciones] (
    [Id] int NOT NULL IDENTITY,
    [ParentId] int NULL,
    [Name] nvarchar(100) NOT NULL,
    [Route] nvarchar(100) NOT NULL,
    [Permission] nvarchar(80) NOT NULL,
    [Sort] int NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_MenuOpciones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MenuOpciones_MenuOpciones_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [MenuOpciones] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Permisos] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(80) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Permisos] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [PoliticasCobranza] (
    [Id] int NOT NULL IDENTITY,
    [Threshold] decimal(18,4) NOT NULL,
    [OverdueDays] int NOT NULL,
    [NoticeDays] int NOT NULL,
    [Enabled] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_PoliticasCobranza] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [PuntosPago] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Kind] nvarchar(30) NOT NULL,
    [Institution] nvarchar(100) NOT NULL,
    [Address] nvarchar(250) NOT NULL,
    [Hours] nvarchar(150) NOT NULL,
    [Methods] nvarchar(50) NOT NULL,
    [Longitude] float NOT NULL,
    [Latitude] float NOT NULL,
    [Active] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_PuntosPago] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Roles] (
    [IdRol] int NOT NULL IDENTITY,
    [NombreRol] nvarchar(50) NOT NULL,
    [Active] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([IdRol])
);
GO


CREATE TABLE [Sectores] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Sectores] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [TarifaVersiones] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [FixedCharge] decimal(18,4) NOT NULL,
    [UnitPrice] decimal(18,4) NOT NULL,
    [Start] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_TarifaVersiones] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [TiposTrabajo] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(40) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Effect] nvarchar(20) NOT NULL,
    [Active] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_TiposTrabajo] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Usuarios] (
    [IdUsuario] int NOT NULL IDENTITY,
    [Login] nvarchar(50) NOT NULL,
    [Nombre] nvarchar(120) NOT NULL,
    [PasswordHash] varbinary(max) NOT NULL,
    [PasswordSalt] varbinary(max) NOT NULL,
    [Iteraciones] int NOT NULL,
    [Activo] bit NOT NULL,
    [MustChangePassword] bit NOT NULL,
    [FailedAttempts] int NOT NULL,
    [LockedUntil] datetime2 NULL,
    [SecurityVersion] int NOT NULL,
    [FechaRegistro] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Usuarios] PRIMARY KEY ([IdUsuario])
);
GO


CREATE TABLE [IncidenciasSIG] (
    [Id] int NOT NULL IDENTITY,
    [ImportId] int NOT NULL,
    [Ordinal] int NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [OriginalJson] nvarchar(max) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_IncidenciasSIG] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IncidenciasSIG_ImportacionesSIG_ImportId] FOREIGN KEY ([ImportId]) REFERENCES [ImportacionesSIG] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Manzanas] (
    [IdManzana] int NOT NULL IDENTITY,
    [IdOrigen] int NULL,
    [UV_MZA] nvarchar(2000) NULL,
    [UV] nvarchar(2000) NULL,
    [MZA] nvarchar(2000) NULL,
    [Geom] geometry NULL,
    [ImportId] int NOT NULL,
    [Ordinal] int NOT NULL,
    [OriginalJson] nvarchar(max) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Manzanas] PRIMARY KEY ([IdManzana]),
    CONSTRAINT [FK_Manzanas_ImportacionesSIG_ImportId] FOREIGN KEY ([ImportId]) REFERENCES [ImportacionesSIG] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Vias] (
    [IdVia] int NOT NULL IDENTITY,
    [OBJECTID] int NULL,
    [Nombre] nvarchar(2000) NULL,
    [TipoVia] nvarchar(2000) NULL,
    [OSMID] nvarchar(2000) NULL,
    [Geom] geometry NULL,
    [ImportId] int NOT NULL,
    [Ordinal] int NOT NULL,
    [OriginalJson] nvarchar(max) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Vias] PRIMARY KEY ([IdVia]),
    CONSTRAINT [FK_Vias_ImportacionesSIG_ImportId] FOREIGN KEY ([ImportId]) REFERENCES [ImportacionesSIG] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [RolesPermisos] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] int NOT NULL,
    [PermissionId] int NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_RolesPermisos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RolesPermisos_Permisos_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permisos] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RolesPermisos_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([IdRol]) ON DELETE NO ACTION
);
GO


CREATE TABLE [TarifaTramos] (
    [Id] int NOT NULL IDENTITY,
    [TariffId] int NOT NULL,
    [From] decimal(18,4) NOT NULL,
    [To] decimal(18,4) NULL,
    [Price] decimal(18,4) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_TarifaTramos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TarifaTramos_TarifaVersiones_TariffId] FOREIGN KEY ([TariffId]) REFERENCES [TarifaVersiones] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ActividadesTrabajo] (
    [Id] int NOT NULL IDENTITY,
    [WorkTypeId] int NOT NULL,
    [Sort] int NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Required] bit NOT NULL,
    [EvidenceRequired] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_ActividadesTrabajo] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ActividadesTrabajo_TiposTrabajo_WorkTypeId] FOREIGN KEY ([WorkTypeId]) REFERENCES [TiposTrabajo] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Bitacora] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NULL,
    [Action] nvarchar(100) NOT NULL,
    [Resource] nvarchar(100) NOT NULL,
    [Detail] nvarchar(2000) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Bitacora] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Bitacora_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [IntentosPago] (
    [Id] int NOT NULL IDENTITY,
    [AccountId] int NOT NULL,
    [UserId] int NOT NULL,
    [Key] nvarchar(80) NOT NULL,
    [Method] nvarchar(12) NOT NULL,
    [Provider] nvarchar(30) NOT NULL,
    [Amount] decimal(18,4) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CheckoutUrl] nvarchar(max) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_IntentosPago] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IntentosPago_CuentasServicio_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [CuentasServicio] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_IntentosPago_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [UsuarioMenu] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [MenuId] int NOT NULL,
    [View] bit NOT NULL,
    [Create] bit NOT NULL,
    [Edit] bit NOT NULL,
    [Delete] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_UsuarioMenu] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UsuarioMenu_MenuOpciones_MenuId] FOREIGN KEY ([MenuId]) REFERENCES [MenuOpciones] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UsuarioMenu_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [UsuarioPermisos] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [PermissionId] int NOT NULL,
    [Allow] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_UsuarioPermisos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UsuarioPermisos_Permisos_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permisos] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UsuarioPermisos_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [UsuariosClientes] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ClientId] int NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_UsuariosClientes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UsuariosClientes_Clientes_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clientes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UsuariosClientes_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [UsuariosRoles] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [RoleId] int NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_UsuariosRoles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UsuariosRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([IdRol]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UsuariosRoles_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Lotes] (
    [IdLote] int NOT NULL IDENTITY,
    [IdOrigen] int NULL,
    [NroLote] nvarchar(2000) NULL,
    [IdManzana] int NULL,
    [Geom] geometry NULL,
    [ImportId] int NOT NULL,
    [Ordinal] int NOT NULL,
    [OriginalJson] nvarchar(max) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Lotes] PRIMARY KEY ([IdLote]),
    CONSTRAINT [FK_Lotes_ImportacionesSIG_ImportId] FOREIGN KEY ([ImportId]) REFERENCES [ImportacionesSIG] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Lotes_Manzanas_IdManzana] FOREIGN KEY ([IdManzana]) REFERENCES [Manzanas] ([IdManzana]) ON DELETE NO ACTION
);
GO


CREATE TABLE [EventosPago] (
    [Id] int NOT NULL IDENTITY,
    [ExternalId] nvarchar(120) NOT NULL,
    [IntentId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_EventosPago] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EventosPago_IntentosPago_IntentId] FOREIGN KEY ([IntentId]) REFERENCES [IntentosPago] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Pagos] (
    [Id] int NOT NULL IDENTITY,
    [AccountId] int NOT NULL,
    [IntentId] int NOT NULL,
    [Reference] nvarchar(120) NOT NULL,
    [Amount] decimal(18,4) NOT NULL,
    [Method] nvarchar(12) NOT NULL,
    [ConfirmedAt] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Pagos] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Payment_Positive] CHECK ([Amount]>0),
    CONSTRAINT [FK_Pagos_CuentasServicio_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [CuentasServicio] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Pagos_IntentosPago_IntentId] FOREIGN KEY ([IntentId]) REFERENCES [IntentosPago] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [CodigosFijos] (
    [IdCodigo] int NOT NULL IDENTITY,
    [CodF_SQL] int NULL,
    [CodF_SIG] nvarchar(2000) NULL,
    [CodFijo] int NULL,
    [Nombre] nvarchar(2000) NULL,
    [Estado] tinyint NOT NULL,
    [FechaCambioEstado] datetime2 NOT NULL,
    [IdLote] int NULL,
    [Longitud] float NULL,
    [Latitud] float NULL,
    [Geom] geometry NULL,
    [ImportId] int NOT NULL,
    [Ordinal] int NOT NULL,
    [OriginalJson] nvarchar(max) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_CodigosFijos] PRIMARY KEY ([IdCodigo]),
    CONSTRAINT [FK_CodigosFijos_ImportacionesSIG_ImportId] FOREIGN KEY ([ImportId]) REFERENCES [ImportacionesSIG] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CodigosFijos_Lotes_IdLote] FOREIGN KEY ([IdLote]) REFERENCES [Lotes] ([IdLote]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ReversionesPago] (
    [Id] int NOT NULL IDENTITY,
    [PaymentId] int NOT NULL,
    [Reference] nvarchar(120) NOT NULL,
    [Reason] nvarchar(500) NOT NULL,
    [UserId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_ReversionesPago] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReversionesPago_Pagos_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Pagos] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReversionesPago_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Conexiones] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(40) NOT NULL,
    [FixedCodeId] int NULL,
    [SectorId] int NULL,
    [RoadId] int NULL,
    [Address] nvarchar(250) NOT NULL,
    [Longitude] float NOT NULL,
    [Latitude] float NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Conexiones] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Connection_Coordinates] CHECK ([Longitude] BETWEEN -180 AND 180 AND [Latitude] BETWEEN -90 AND 90),
    CONSTRAINT [FK_Conexiones_CodigosFijos_FixedCodeId] FOREIGN KEY ([FixedCodeId]) REFERENCES [CodigosFijos] ([IdCodigo]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Conexiones_Sectores_SectorId] FOREIGN KEY ([SectorId]) REFERENCES [Sectores] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Conexiones_Vias_RoadId] FOREIGN KEY ([RoadId]) REFERENCES [Vias] ([IdVia]) ON DELETE NO ACTION
);
GO


CREATE TABLE [ContratosServicio] (
    [Id] int NOT NULL IDENTITY,
    [AccountId] int NOT NULL,
    [ClientId] int NOT NULL,
    [ConnectionId] int NOT NULL,
    [TariffId] int NOT NULL,
    [Start] datetime2 NOT NULL,
    [End] datetime2 NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_ContratosServicio] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ContratosServicio_Clientes_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clientes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ContratosServicio_Conexiones_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [Conexiones] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ContratosServicio_CuentasServicio_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [CuentasServicio] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ContratosServicio_TarifaVersiones_TariffId] FOREIGN KEY ([TariffId]) REFERENCES [TarifaVersiones] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [InstalacionesMedidor] (
    [Id] int NOT NULL IDENTITY,
    [ConnectionId] int NOT NULL,
    [MeterId] int NOT NULL,
    [Start] datetime2 NOT NULL,
    [End] datetime2 NULL,
    [InitialReading] decimal(18,4) NOT NULL,
    [FinalReading] decimal(18,4) NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_InstalacionesMedidor] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InstalacionesMedidor_Conexiones_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [Conexiones] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InstalacionesMedidor_Medidores_MeterId] FOREIGN KEY ([MeterId]) REFERENCES [Medidores] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [OrdenesServicio] (
    [IdOrden] int NOT NULL IDENTITY,
    [Number] nvarchar(40) NOT NULL,
    [WorkTypeId] int NOT NULL,
    [Effect] nvarchar(20) NOT NULL,
    [ContractId] int NULL,
    [ConnectionId] int NULL,
    [SupervisorId] int NOT NULL,
    [Priority] nvarchar(20) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ScheduledAt] datetime2 NOT NULL,
    [StartedAt] datetime2 NULL,
    [FinishedAt] datetime2 NULL,
    [EffectAt] datetime2 NULL,
    [Latitude] float NOT NULL,
    [Longitude] float NOT NULL,
    [Instructions] nvarchar(2000) NOT NULL,
    [Observations] nvarchar(2000) NOT NULL,
    [CutAuthorizedAt] datetime2 NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_OrdenesServicio] PRIMARY KEY ([IdOrden]),
    CONSTRAINT [CK_Order_State] CHECK ([Status] IN ('PENDIENTE','ASIGNADA','EN_CAMINO','EN_EJECUCION','FINALIZADA','VERIFICADA','CERRADA','CANCELADA','NO_REALIZADA','REPROGRAMADA')),
    CONSTRAINT [FK_OrdenesServicio_Conexiones_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [Conexiones] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrdenesServicio_ContratosServicio_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [ContratosServicio] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrdenesServicio_TiposTrabajo_WorkTypeId] FOREIGN KEY ([WorkTypeId]) REFERENCES [TiposTrabajo] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrdenesServicio_Usuarios_SupervisorId] FOREIGN KEY ([SupervisorId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Lecturas] (
    [Id] int NOT NULL IDENTITY,
    [InstallationId] int NOT NULL,
    [Period] nvarchar(7) NOT NULL,
    [Value] decimal(18,4) NOT NULL,
    [PreviousValue] decimal(18,4) NOT NULL,
    [TakenAt] datetime2 NOT NULL,
    [UserId] int NOT NULL,
    [Estimated] bit NOT NULL,
    [Note] nvarchar(500) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Lecturas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Lecturas_InstalacionesMedidor_InstallationId] FOREIGN KEY ([InstallationId]) REFERENCES [InstalacionesMedidor] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Lecturas_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Asignaciones] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [OperatorId] int NOT NULL,
    [AssignedById] int NOT NULL,
    [Start] datetime2 NOT NULL,
    [End] datetime2 NULL,
    [Reason] nvarchar(500) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Asignaciones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Asignaciones_OrdenesServicio_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [OrdenesServicio] ([IdOrden]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Asignaciones_Usuarios_AssignedById] FOREIGN KEY ([AssignedById]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Asignaciones_Usuarios_OperatorId] FOREIGN KEY ([OperatorId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [AvisosCorte] (
    [Id] int NOT NULL IDENTITY,
    [ContractId] int NOT NULL,
    [OrderId] int NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ScheduledAt] datetime2 NOT NULL,
    [DebtAtIssue] decimal(18,4) NOT NULL,
    [PolicyId] int NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_AvisosCorte] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AvisosCorte_ContratosServicio_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [ContratosServicio] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AvisosCorte_OrdenesServicio_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [OrdenesServicio] ([IdOrden]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AvisosCorte_PoliticasCobranza_PolicyId] FOREIGN KEY ([PolicyId]) REFERENCES [PoliticasCobranza] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [EventosOrden] (
    [IdEvento] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [UserId] int NOT NULL,
    [Action] nvarchar(40) NOT NULL,
    [Detail] nvarchar(2000) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_EventosOrden] PRIMARY KEY ([IdEvento]),
    CONSTRAINT [FK_EventosOrden_OrdenesServicio_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [OrdenesServicio] ([IdOrden]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EventosOrden_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Notificaciones] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [OrderId] int NULL,
    [Message] nvarchar(500) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ReadAt] datetime2 NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Notificaciones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Notificaciones_OrdenesServicio_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [OrdenesServicio] ([IdOrden]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Notificaciones_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [OrdenActividades] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [Sort] int NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Required] bit NOT NULL,
    [EvidenceRequired] bit NOT NULL,
    [Done] bit NOT NULL,
    [Result] nvarchar(1000) NOT NULL,
    [UserId] int NULL,
    [DoneAt] datetime2 NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_OrdenActividades] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrdenActividades_OrdenesServicio_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [OrdenesServicio] ([IdOrden]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrdenActividades_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [OrdenMateriales] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [MaterialId] int NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [UnitCost] decimal(18,4) NOT NULL,
    [UserId] int NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_OrdenMateriales] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Material_Positive] CHECK ([Quantity]>0 AND [UnitCost]>=0),
    CONSTRAINT [FK_OrdenMateriales_Materiales_MaterialId] FOREIGN KEY ([MaterialId]) REFERENCES [Materiales] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrdenMateriales_OrdenesServicio_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [OrdenesServicio] ([IdOrden]) ON DELETE NO ACTION,
    CONSTRAINT [FK_OrdenMateriales_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Facturas] (
    [Id] int NOT NULL IDENTITY,
    [Number] nvarchar(40) NOT NULL,
    [ContractId] int NOT NULL,
    [ReadingId] int NULL,
    [Period] nvarchar(7) NOT NULL,
    [IssuedAt] datetime2 NOT NULL,
    [DueAt] datetime2 NOT NULL,
    [Consumption] decimal(18,4) NOT NULL,
    [Total] decimal(18,4) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [HolderName] nvarchar(150) NOT NULL,
    [HolderAddress] nvarchar(250) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Facturas] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Invoice_Total] CHECK ([Total]>=0 AND [Consumption]>=0),
    CONSTRAINT [FK_Facturas_ContratosServicio_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [ContratosServicio] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Facturas_Lecturas_ReadingId] FOREIGN KEY ([ReadingId]) REFERENCES [Lecturas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Evidencias] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [ActivityId] int NULL,
    [UserId] int NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [StorageKey] nvarchar(100) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [Hash] nvarchar(64) NOT NULL,
    [Size] bigint NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Evidencias] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Evidencias_OrdenActividades_ActivityId] FOREIGN KEY ([ActivityId]) REFERENCES [OrdenActividades] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Evidencias_OrdenesServicio_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [OrdenesServicio] ([IdOrden]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Evidencias_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [AjustesFactura] (
    [Id] int NOT NULL IDENTITY,
    [InvoiceId] int NOT NULL,
    [Amount] decimal(18,4) NOT NULL,
    [Reason] nvarchar(500) NOT NULL,
    [UserId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_AjustesFactura] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AjustesFactura_Facturas_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Facturas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AjustesFactura_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([IdUsuario]) ON DELETE NO ACTION
);
GO


CREATE TABLE [AvisoFacturas] (
    [Id] int NOT NULL IDENTITY,
    [NoticeId] int NOT NULL,
    [InvoiceId] int NOT NULL,
    [Balance] decimal(18,4) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_AvisoFacturas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AvisoFacturas_AvisosCorte_NoticeId] FOREIGN KEY ([NoticeId]) REFERENCES [AvisosCorte] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AvisoFacturas_Facturas_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Facturas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [FacturaDetalles] (
    [Id] int NOT NULL IDENTITY,
    [InvoiceId] int NOT NULL,
    [Description] nvarchar(150) NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [UnitPrice] decimal(18,4) NOT NULL,
    [Amount] decimal(18,4) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_FacturaDetalles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FacturaDetalles_Facturas_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Facturas] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [PagoAplicaciones] (
    [Id] int NOT NULL IDENTITY,
    [PaymentId] int NOT NULL,
    [InvoiceId] int NOT NULL,
    [Amount] decimal(18,4) NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_PagoAplicaciones] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Allocation_Positive] CHECK ([Amount]>0),
    CONSTRAINT [FK_PagoAplicaciones_Facturas_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Facturas] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PagoAplicaciones_Pagos_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Pagos] ([Id]) ON DELETE NO ACTION
);
GO


CREATE INDEX [IX_ActividadesTrabajo_WorkTypeId] ON [ActividadesTrabajo] ([WorkTypeId]);
GO


CREATE INDEX [IX_AjustesFactura_InvoiceId] ON [AjustesFactura] ([InvoiceId]);
GO


CREATE INDEX [IX_AjustesFactura_UserId] ON [AjustesFactura] ([UserId]);
GO


CREATE INDEX [IX_Asignaciones_AssignedById] ON [Asignaciones] ([AssignedById]);
GO


CREATE INDEX [IX_Asignaciones_OperatorId] ON [Asignaciones] ([OperatorId]);
GO


CREATE UNIQUE INDEX [IX_Asignaciones_OrderId] ON [Asignaciones] ([OrderId]) WHERE [End] IS NULL;
GO


CREATE INDEX [IX_AvisoFacturas_InvoiceId] ON [AvisoFacturas] ([InvoiceId]);
GO


CREATE UNIQUE INDEX [IX_AvisoFacturas_NoticeId_InvoiceId] ON [AvisoFacturas] ([NoticeId], [InvoiceId]);
GO


CREATE UNIQUE INDEX [IX_AvisosCorte_ContractId] ON [AvisosCorte] ([ContractId]) WHERE [Status] = 'VIGENTE';
GO


CREATE INDEX [IX_AvisosCorte_OrderId] ON [AvisosCorte] ([OrderId]);
GO


CREATE INDEX [IX_AvisosCorte_PolicyId] ON [AvisosCorte] ([PolicyId]);
GO


CREATE INDEX [IX_Bitacora_UserId] ON [Bitacora] ([UserId]);
GO


CREATE INDEX [IX_CodigosFijos_IdLote] ON [CodigosFijos] ([IdLote]);
GO


CREATE UNIQUE INDEX [IX_CodigosFijos_ImportId_Ordinal] ON [CodigosFijos] ([ImportId], [Ordinal]);
GO


CREATE UNIQUE INDEX [IX_Conexiones_Code] ON [Conexiones] ([Code]);
GO


CREATE INDEX [IX_Conexiones_FixedCodeId] ON [Conexiones] ([FixedCodeId]);
GO


CREATE INDEX [IX_Conexiones_RoadId] ON [Conexiones] ([RoadId]);
GO


CREATE INDEX [IX_Conexiones_SectorId] ON [Conexiones] ([SectorId]);
GO


CREATE UNIQUE INDEX [IX_ContratosServicio_AccountId] ON [ContratosServicio] ([AccountId]) WHERE [End] IS NULL;
GO


CREATE INDEX [IX_ContratosServicio_ClientId] ON [ContratosServicio] ([ClientId]);
GO


CREATE UNIQUE INDEX [IX_ContratosServicio_ConnectionId] ON [ContratosServicio] ([ConnectionId]) WHERE [End] IS NULL;
GO


CREATE INDEX [IX_ContratosServicio_TariffId] ON [ContratosServicio] ([TariffId]);
GO


CREATE UNIQUE INDEX [IX_CuentasServicio_Number] ON [CuentasServicio] ([Number]);
GO


CREATE INDEX [IX_EventosOrden_OrderId] ON [EventosOrden] ([OrderId]);
GO


CREATE INDEX [IX_EventosOrden_UserId] ON [EventosOrden] ([UserId]);
GO


CREATE UNIQUE INDEX [IX_EventosPago_ExternalId] ON [EventosPago] ([ExternalId]);
GO


CREATE INDEX [IX_EventosPago_IntentId] ON [EventosPago] ([IntentId]);
GO


CREATE INDEX [IX_Evidencias_ActivityId] ON [Evidencias] ([ActivityId]);
GO


CREATE INDEX [IX_Evidencias_OrderId] ON [Evidencias] ([OrderId]);
GO


CREATE INDEX [IX_Evidencias_UserId] ON [Evidencias] ([UserId]);
GO


CREATE INDEX [IX_FacturaDetalles_InvoiceId] ON [FacturaDetalles] ([InvoiceId]);
GO


CREATE UNIQUE INDEX [IX_Facturas_ContractId_Period] ON [Facturas] ([ContractId], [Period]);
GO


CREATE INDEX [IX_Facturas_DueAt] ON [Facturas] ([DueAt]);
GO


CREATE UNIQUE INDEX [IX_Facturas_Number] ON [Facturas] ([Number]);
GO


CREATE INDEX [IX_Facturas_ReadingId] ON [Facturas] ([ReadingId]);
GO


CREATE UNIQUE INDEX [IX_ImportacionesSIG_Layer_Hash] ON [ImportacionesSIG] ([Layer], [Hash]);
GO


CREATE INDEX [IX_IncidenciasSIG_ImportId] ON [IncidenciasSIG] ([ImportId]);
GO


CREATE UNIQUE INDEX [IX_InstalacionesMedidor_ConnectionId] ON [InstalacionesMedidor] ([ConnectionId]) WHERE [End] IS NULL;
GO


CREATE UNIQUE INDEX [IX_InstalacionesMedidor_MeterId] ON [InstalacionesMedidor] ([MeterId]) WHERE [End] IS NULL;
GO


CREATE INDEX [IX_IntentosPago_AccountId] ON [IntentosPago] ([AccountId]);
GO


CREATE UNIQUE INDEX [IX_IntentosPago_Key] ON [IntentosPago] ([Key]);
GO


CREATE INDEX [IX_IntentosPago_UserId] ON [IntentosPago] ([UserId]);
GO


CREATE UNIQUE INDEX [IX_Lecturas_InstallationId_Period] ON [Lecturas] ([InstallationId], [Period]);
GO


CREATE INDEX [IX_Lecturas_UserId] ON [Lecturas] ([UserId]);
GO


CREATE INDEX [IX_Lotes_IdManzana] ON [Lotes] ([IdManzana]);
GO


CREATE UNIQUE INDEX [IX_Lotes_ImportId_Ordinal] ON [Lotes] ([ImportId], [Ordinal]);
GO


CREATE UNIQUE INDEX [IX_Manzanas_ImportId_Ordinal] ON [Manzanas] ([ImportId], [Ordinal]);
GO


CREATE UNIQUE INDEX [IX_Materiales_Code] ON [Materiales] ([Code]);
GO


CREATE UNIQUE INDEX [IX_Medidores_Serial] ON [Medidores] ([Serial]);
GO


CREATE INDEX [IX_MenuOpciones_ParentId] ON [MenuOpciones] ([ParentId]);
GO


CREATE INDEX [IX_Notificaciones_OrderId] ON [Notificaciones] ([OrderId]);
GO


CREATE INDEX [IX_Notificaciones_UserId_ReadAt] ON [Notificaciones] ([UserId], [ReadAt]);
GO


CREATE INDEX [IX_OrdenActividades_OrderId] ON [OrdenActividades] ([OrderId]);
GO


CREATE INDEX [IX_OrdenActividades_UserId] ON [OrdenActividades] ([UserId]);
GO


CREATE INDEX [IX_OrdenesServicio_ConnectionId] ON [OrdenesServicio] ([ConnectionId]);
GO


CREATE INDEX [IX_OrdenesServicio_ContractId] ON [OrdenesServicio] ([ContractId]);
GO


CREATE UNIQUE INDEX [IX_OrdenesServicio_Number] ON [OrdenesServicio] ([Number]);
GO


CREATE INDEX [IX_OrdenesServicio_Status_ScheduledAt] ON [OrdenesServicio] ([Status], [ScheduledAt]);
GO


CREATE INDEX [IX_OrdenesServicio_SupervisorId] ON [OrdenesServicio] ([SupervisorId]);
GO


CREATE INDEX [IX_OrdenesServicio_WorkTypeId] ON [OrdenesServicio] ([WorkTypeId]);
GO


CREATE INDEX [IX_OrdenMateriales_MaterialId] ON [OrdenMateriales] ([MaterialId]);
GO


CREATE INDEX [IX_OrdenMateriales_OrderId] ON [OrdenMateriales] ([OrderId]);
GO


CREATE INDEX [IX_OrdenMateriales_UserId] ON [OrdenMateriales] ([UserId]);
GO


CREATE INDEX [IX_PagoAplicaciones_InvoiceId] ON [PagoAplicaciones] ([InvoiceId]);
GO


CREATE INDEX [IX_PagoAplicaciones_PaymentId] ON [PagoAplicaciones] ([PaymentId]);
GO


CREATE INDEX [IX_Pagos_AccountId] ON [Pagos] ([AccountId]);
GO


CREATE UNIQUE INDEX [IX_Pagos_IntentId] ON [Pagos] ([IntentId]);
GO


CREATE UNIQUE INDEX [IX_Pagos_Reference] ON [Pagos] ([Reference]);
GO


CREATE UNIQUE INDEX [IX_Permisos_Code] ON [Permisos] ([Code]);
GO


CREATE UNIQUE INDEX [IX_ReversionesPago_PaymentId] ON [ReversionesPago] ([PaymentId]);
GO


CREATE UNIQUE INDEX [IX_ReversionesPago_Reference] ON [ReversionesPago] ([Reference]);
GO


CREATE INDEX [IX_ReversionesPago_UserId] ON [ReversionesPago] ([UserId]);
GO


CREATE UNIQUE INDEX [IX_Roles_NombreRol] ON [Roles] ([NombreRol]);
GO


CREATE INDEX [IX_RolesPermisos_PermissionId] ON [RolesPermisos] ([PermissionId]);
GO


CREATE UNIQUE INDEX [IX_RolesPermisos_RoleId_PermissionId] ON [RolesPermisos] ([RoleId], [PermissionId]);
GO


CREATE UNIQUE INDEX [IX_Sectores_Code] ON [Sectores] ([Code]);
GO


CREATE INDEX [IX_TarifaTramos_TariffId] ON [TarifaTramos] ([TariffId]);
GO


CREATE UNIQUE INDEX [IX_TiposTrabajo_Code] ON [TiposTrabajo] ([Code]);
GO


CREATE INDEX [IX_UsuarioMenu_MenuId] ON [UsuarioMenu] ([MenuId]);
GO


CREATE UNIQUE INDEX [IX_UsuarioMenu_UserId_MenuId] ON [UsuarioMenu] ([UserId], [MenuId]);
GO


CREATE INDEX [IX_UsuarioPermisos_PermissionId] ON [UsuarioPermisos] ([PermissionId]);
GO


CREATE UNIQUE INDEX [IX_UsuarioPermisos_UserId_PermissionId] ON [UsuarioPermisos] ([UserId], [PermissionId]);
GO


CREATE UNIQUE INDEX [IX_Usuarios_Login] ON [Usuarios] ([Login]);
GO


CREATE INDEX [IX_UsuariosClientes_ClientId] ON [UsuariosClientes] ([ClientId]);
GO


CREATE UNIQUE INDEX [IX_UsuariosClientes_UserId_ClientId] ON [UsuariosClientes] ([UserId], [ClientId]);
GO


CREATE INDEX [IX_UsuariosRoles_RoleId] ON [UsuariosRoles] ([RoleId]);
GO


CREATE UNIQUE INDEX [IX_UsuariosRoles_UserId_RoleId] ON [UsuariosRoles] ([UserId], [RoleId]);
GO


CREATE UNIQUE INDEX [IX_Vias_ImportId_Ordinal] ON [Vias] ([ImportId], [Ordinal]);
GO



