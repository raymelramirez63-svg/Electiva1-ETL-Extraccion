:ON ERROR EXIT
/* SQL Server 2019+. Ejecutar con sqlcmd -b o en SSMS con modo SQLCMD.
   Base nueva: no elimina ni modifica AnalisisOpinionesDB.
   Ejecutar una sola vez. Si existe, se detiene sin borrar objetos. */
USE master;
GO
IF DB_ID(N'AnalisisOpinionesDW') IS NOT NULL
    THROW 50001, 'AnalisisOpinionesDW ya existe. Revisar antes de desplegar.', 1;
GO
-- Con sqlcmd usar -b para detenerse ante errores.
IF DB_ID(N'AnalisisOpinionesDW') IS NULL
    EXEC(N'CREATE DATABASE AnalisisOpinionesDW');
GO
USE AnalisisOpinionesDW;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
CREATE TABLE dbo.DimFecha (
    FechaKey int NOT NULL PRIMARY KEY,
    Fecha date NOT NULL UNIQUE,
    Anio smallint NOT NULL,
    Trimestre tinyint NOT NULL CHECK (Trimestre BETWEEN 1 AND 4),
    Mes tinyint NOT NULL CHECK (Mes BETWEEN 1 AND 12),
    Dia tinyint NOT NULL CHECK (Dia BETWEEN 1 AND 31),
    CHECK (FechaKey = YEAR(Fecha)*10000+MONTH(Fecha)*100+DAY(Fecha)),
    CHECK (Anio=YEAR(Fecha) AND Trimestre=DATEPART(quarter,Fecha)
        AND Mes=MONTH(Fecha) AND Dia=DAY(Fecha))
);
CREATE TABLE dbo.DimCliente (
    ClienteKey int IDENTITY NOT NULL PRIMARY KEY,
    IdClienteOrigen int NOT NULL UNIQUE,
    Nombre nvarchar(150) NOT NULL CHECK (LEN(LTRIM(RTRIM(Nombre)))>0)
);
CREATE TABLE dbo.DimProducto (
    ProductoKey int IDENTITY NOT NULL PRIMARY KEY,
    IdProductoOrigen int NOT NULL UNIQUE,
    Nombre nvarchar(150) NOT NULL CHECK (LEN(LTRIM(RTRIM(Nombre)))>0),
    Categoria nvarchar(100) NOT NULL CHECK (LEN(LTRIM(RTRIM(Categoria)))>0)
);
CREATE TABLE dbo.DimCanal (
    CanalKey tinyint NOT NULL PRIMARY KEY,
    Codigo nvarchar(20) NOT NULL UNIQUE,
    Nombre nvarchar(100) NOT NULL
);
CREATE TABLE dbo.DimClasificacion (
    ClasificacionKey tinyint NOT NULL PRIMARY KEY,
    Nombre nvarchar(20) NOT NULL UNIQUE
        CHECK (Nombre IN (N'Positiva',N'Negativa',N'Neutra'))
);
CREATE TABLE dbo.FactOpinion (
    OpinionKey bigint IDENTITY NOT NULL PRIMARY KEY,
    IdOpinionOrigen bigint NOT NULL UNIQUE,
    IdOrigenExterno nvarchar(100) NOT NULL CHECK (LEN(LTRIM(RTRIM(IdOrigenExterno)))>0),
    IdCargaOrigen bigint NOT NULL,
    FechaKey int NOT NULL REFERENCES dbo.DimFecha(FechaKey),
    ClienteKey int NOT NULL REFERENCES dbo.DimCliente(ClienteKey),
    ProductoKey int NOT NULL REFERENCES dbo.DimProducto(ProductoKey),
    CanalKey tinyint NOT NULL REFERENCES dbo.DimCanal(CanalKey),
    ClasificacionKey tinyint NOT NULL REFERENCES dbo.DimClasificacion(ClasificacionKey),
    PuntajeSatisfaccion decimal(5,2) NOT NULL CHECK (PuntajeSatisfaccion BETWEEN 0 AND 100),
    EsPuntajeEstimado bit NOT NULL,
    FechaCargaDW datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_FactOpinion_Origen UNIQUE(CanalKey,IdOrigenExterno)
);
-- El comentario permanece en OLTP: los KPI no necesitan leer textos de 2,000 caracteres.
CREATE INDEX IX_FactOpinion_ProductoFecha ON dbo.FactOpinion(ProductoKey,FechaKey)
    INCLUDE (CanalKey,ClasificacionKey,PuntajeSatisfaccion,EsPuntajeEstimado);
CREATE INDEX IX_FactOpinion_FechaCanal ON dbo.FactOpinion(FechaKey,CanalKey)
    INCLUDE (ProductoKey,ClienteKey,ClasificacionKey,PuntajeSatisfaccion,EsPuntajeEstimado);
-- Auditoria de cada intento de carga analitica (incluye los que terminan con error).
-- No es una sexta dimension: es una tabla administrativa fuera de la estrella.
CREATE TABLE dbo.EjecucionesCargaDW (
    IdEjecucion bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_EjecucionesCargaDW PRIMARY KEY,
    IdCargaOrigen bigint NOT NULL,
    FechaInicioUTC datetime2(3) NOT NULL CONSTRAINT DF_EjecDW_Inicio DEFAULT SYSUTCDATETIME(),
    FechaFinUTC datetime2(3) NULL,
    Estado nvarchar(15) NOT NULL CONSTRAINT CK_EjecDW_Estado CHECK (Estado IN (N'EnProceso',N'Completada',N'Fallida')),
    RegistrosOrigen bigint NOT NULL CONSTRAINT DF_EjecDW_Origen DEFAULT (0),
    RegistrosInsertados bigint NOT NULL CONSTRAINT DF_EjecDW_Insertados DEFAULT (0),
    RegistrosOmitidos bigint NOT NULL CONSTRAINT DF_EjecDW_Omitidos DEFAULT (0),
    DuracionMs bigint NULL,
    Mensaje nvarchar(2000) NOT NULL CONSTRAINT DF_EjecDW_Mensaje DEFAULT (N''),
    CONSTRAINT CK_EjecDW_Contadores CHECK (
       RegistrosOrigen >= 0 AND RegistrosInsertados >= 0 AND RegistrosOmitidos >= 0
    )
);
CREATE INDEX IX_EjecucionesCargaDW_Lote_Fecha
    ON dbo.EjecucionesCargaDW (IdCargaOrigen, FechaInicioUTC DESC)
    INCLUDE (Estado, RegistrosOrigen, RegistrosInsertados, RegistrosOmitidos, DuracionMs);
-- El indice por IdCargaOrigen agiliza conciliacion/auditoria por lote.
CREATE INDEX IX_FactOpinion_IdCargaOrigen
    ON dbo.FactOpinion (IdCargaOrigen) INCLUDE (IdOpinionOrigen);
COMMIT;
GO
