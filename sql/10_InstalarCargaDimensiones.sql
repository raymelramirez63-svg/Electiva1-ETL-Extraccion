USE AnalisisOpinionesDW;
GO
CREATE OR ALTER VIEW dbo.vw_FechasOpinionOrigen
AS
    SELECT DISTINCT o.FechaOpinion AS Fecha
    FROM AnalisisOpinionesDB.dbo.Opiniones AS o
    JOIN AnalisisOpinionesDB.dbo.CargasETL AS c ON c.IdCarga = o.IdCarga
    WHERE c.Estado = N'Completada';
GO
CREATE OR ALTER PROCEDURE dbo.usp_CargarDimensiones
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT <> 0
        THROW 50200, 'Ejecutar la carga fuera de una transaccion abierta.', 1;

    DECLARE @Resumen TABLE (
        Orden tinyint PRIMARY KEY,
        Dimension sysname NOT NULL,
        Insertadas int NOT NULL,
        Actualizadas int NOT NULL,
        Total bigint NULL
    );
    DECLARE @Bloqueo int, @Actualizadas int;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Usa el mismo bloqueo que usp_CargarOpiniones.
        EXEC @Bloqueo = sys.sp_getapplock
            @Resource = N'AnalisisOpinionesDW_usp_CargarOpiniones',
            @LockMode = 'Exclusive', @LockOwner = 'Transaction',
            @LockTimeout = 30000;
        IF @Bloqueo < 0
            THROW 50201, 'No se pudo obtener el bloqueo de carga.', 1;

        INSERT dbo.DimFecha (FechaKey, Fecha, Anio, Trimestre, Mes, Dia)
        SELECT CONVERT(int, CONVERT(char(8), s.Fecha, 112)), s.Fecha,
               YEAR(s.Fecha), DATEPART(quarter, s.Fecha), MONTH(s.Fecha), DAY(s.Fecha)
        FROM dbo.vw_FechasOpinionOrigen AS s
        WHERE NOT EXISTS (SELECT 1 FROM dbo.DimFecha AS d WHERE d.Fecha = s.Fecha);
        INSERT @Resumen VALUES (1, N'DimFecha', @@ROWCOUNT, 0, NULL);

        UPDATE d SET Nombre = s.Nombre
        FROM dbo.DimCliente AS d
        JOIN AnalisisOpinionesDB.dbo.Clientes AS s ON s.IdCliente = d.IdClienteOrigen
        WHERE CONVERT(varbinary(300), d.Nombre) <> CONVERT(varbinary(300), s.Nombre);
        SET @Actualizadas = @@ROWCOUNT;

        INSERT dbo.DimCliente (IdClienteOrigen, Nombre)
        SELECT s.IdCliente, s.Nombre
        FROM AnalisisOpinionesDB.dbo.Clientes AS s
        WHERE NOT EXISTS (SELECT 1 FROM dbo.DimCliente AS d WHERE d.IdClienteOrigen = s.IdCliente);
        INSERT @Resumen VALUES (2, N'DimCliente', @@ROWCOUNT, @Actualizadas, NULL);

        SELECT p.IdProducto, p.Nombre, c.Nombre AS Categoria
        INTO #Productos
        FROM AnalisisOpinionesDB.dbo.Productos AS p
        JOIN AnalisisOpinionesDB.dbo.Categorias AS c ON c.IdCategoria = p.IdCategoria;

        UPDATE d SET Nombre = s.Nombre, Categoria = s.Categoria
        FROM dbo.DimProducto AS d
        JOIN #Productos AS s ON s.IdProducto = d.IdProductoOrigen
        WHERE CONVERT(varbinary(300), d.Nombre) <> CONVERT(varbinary(300), s.Nombre)
           OR CONVERT(varbinary(200), d.Categoria) <> CONVERT(varbinary(200), s.Categoria);
        SET @Actualizadas = @@ROWCOUNT;

        INSERT dbo.DimProducto (IdProductoOrigen, Nombre, Categoria)
        SELECT s.IdProducto, s.Nombre, s.Categoria
        FROM #Productos AS s
        WHERE NOT EXISTS (SELECT 1 FROM dbo.DimProducto AS d WHERE d.IdProductoOrigen = s.IdProducto);
        INSERT @Resumen VALUES (3, N'DimProducto', @@ROWCOUNT, @Actualizadas, NULL);

        UPDATE d SET Codigo = s.Codigo, Nombre = s.Descripcion
        FROM dbo.DimCanal AS d
        JOIN AnalisisOpinionesDB.dbo.Canales AS s ON s.IdCanal = d.CanalKey
        WHERE CONVERT(varbinary(40), d.Codigo) <> CONVERT(varbinary(40), s.Codigo)
           OR CONVERT(varbinary(200), d.Nombre) <> CONVERT(varbinary(200), s.Descripcion);
        SET @Actualizadas = @@ROWCOUNT;

        INSERT dbo.DimCanal (CanalKey, Codigo, Nombre)
        SELECT s.IdCanal, s.Codigo, s.Descripcion
        FROM AnalisisOpinionesDB.dbo.Canales AS s
        WHERE NOT EXISTS (SELECT 1 FROM dbo.DimCanal AS d WHERE d.CanalKey = s.IdCanal);
        INSERT @Resumen VALUES (4, N'DimCanal', @@ROWCOUNT, @Actualizadas, NULL);

        UPDATE d SET Nombre = s.Nombre
        FROM dbo.DimClasificacion AS d
        JOIN AnalisisOpinionesDB.dbo.Clasificaciones AS s ON s.IdClasificacion = d.ClasificacionKey
        WHERE CONVERT(varbinary(40), d.Nombre) <> CONVERT(varbinary(40), s.Nombre);
        SET @Actualizadas = @@ROWCOUNT;

        INSERT dbo.DimClasificacion (ClasificacionKey, Nombre)
        SELECT s.IdClasificacion, s.Nombre
        FROM AnalisisOpinionesDB.dbo.Clasificaciones AS s
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.DimClasificacion AS d WHERE d.ClasificacionKey = s.IdClasificacion
        );
        INSERT @Resumen VALUES (5, N'DimClasificacion', @@ROWCOUNT, @Actualizadas, NULL);

        UPDATE r SET Total = c.Total
        FROM @Resumen AS r
        JOIN (
            SELECT 1 AS Orden, COUNT_BIG(*) AS Total FROM dbo.DimFecha
            UNION ALL SELECT 2, COUNT_BIG(*) FROM dbo.DimCliente
            UNION ALL SELECT 3, COUNT_BIG(*) FROM dbo.DimProducto
            UNION ALL SELECT 4, COUNT_BIG(*) FROM dbo.DimCanal
            UNION ALL SELECT 5, COUNT_BIG(*) FROM dbo.DimClasificacion
        ) AS c ON c.Orden = r.Orden;

        COMMIT TRANSACTION;
        SELECT Dimension, Insertadas, Actualizadas, Total FROM @Resumen ORDER BY Orden;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
