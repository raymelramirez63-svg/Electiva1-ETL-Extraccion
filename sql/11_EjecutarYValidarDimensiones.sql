USE AnalisisOpinionesDW;
GO
SET NOCOUNT ON;

-- Carga inicial y reejecucion para comprobar que no se repiten filas.
EXEC dbo.usp_CargarDimensiones;
EXEC dbo.usp_CargarDimensiones;

-- 1. Fechas de lotes completados frente a sus claves y atributos de calendario.
SELECT N'DimFecha' AS Dimension, (SELECT COUNT_BIG(*) FROM dbo.DimFecha) AS Cargadas,
       COUNT_BIG(*) AS Origen,
       COUNT_BIG(CASE WHEN d.Fecha IS NULL OR d.Anio <> YEAR(s.Fecha)
                      OR d.Trimestre <> DATEPART(quarter, s.Fecha)
                      OR d.Mes <> MONTH(s.Fecha) OR d.Dia <> DAY(s.Fecha) THEN 1 END) AS Diferencias
FROM dbo.vw_FechasOpinionOrigen AS s
LEFT JOIN dbo.DimFecha AS d ON d.Fecha = s.Fecha;

-- 2. Clientes, sin leer el correo electrónico.
SELECT N'DimCliente' AS Dimension, (SELECT COUNT_BIG(*) FROM dbo.DimCliente) AS Cargadas,
       COUNT_BIG(*) AS Origen,
       COUNT_BIG(CASE WHEN d.ClienteKey IS NULL OR
           CONVERT(varbinary(300), d.Nombre) <> CONVERT(varbinary(300), s.Nombre) THEN 1 END) AS Diferencias
FROM AnalisisOpinionesDB.dbo.Clientes AS s
LEFT JOIN dbo.DimCliente AS d ON d.IdClienteOrigen = s.IdCliente;

-- 3. Productos y categoría.
SELECT N'DimProducto' AS Dimension, (SELECT COUNT_BIG(*) FROM dbo.DimProducto) AS Cargadas,
       COUNT_BIG(*) AS Origen,
       COUNT_BIG(CASE WHEN c.IdCategoria IS NULL OR d.ProductoKey IS NULL OR
           CONVERT(varbinary(300), d.Nombre) <> CONVERT(varbinary(300), s.Nombre) OR
           CONVERT(varbinary(200), d.Categoria) <> CONVERT(varbinary(200), c.Nombre) THEN 1 END) AS Diferencias
FROM AnalisisOpinionesDB.dbo.Productos AS s
LEFT JOIN AnalisisOpinionesDB.dbo.Categorias AS c ON c.IdCategoria = s.IdCategoria
LEFT JOIN dbo.DimProducto AS d ON d.IdProductoOrigen = s.IdProducto;

-- 4. Catálogo de canales.
SELECT N'DimCanal' AS Dimension, (SELECT COUNT_BIG(*) FROM dbo.DimCanal) AS Cargadas,
       COUNT_BIG(*) AS Origen,
       COUNT_BIG(CASE WHEN d.CanalKey IS NULL OR
           CONVERT(varbinary(40), d.Codigo) <> CONVERT(varbinary(40), s.Codigo) OR
           CONVERT(varbinary(200), d.Nombre) <> CONVERT(varbinary(200), s.Descripcion) THEN 1 END) AS Diferencias
FROM AnalisisOpinionesDB.dbo.Canales AS s
LEFT JOIN dbo.DimCanal AS d ON d.CanalKey = s.IdCanal;

-- 5. Catálogo de clasificaciones.
SELECT N'DimClasificacion' AS Dimension,
       (SELECT COUNT_BIG(*) FROM dbo.DimClasificacion) AS Cargadas,
       COUNT_BIG(*) AS Origen,
       COUNT_BIG(CASE WHEN d.ClasificacionKey IS NULL OR
           CONVERT(varbinary(40), d.Nombre) <> CONVERT(varbinary(40), s.Nombre) THEN 1 END) AS Diferencias
FROM AnalisisOpinionesDB.dbo.Clasificaciones AS s
LEFT JOIN dbo.DimClasificacion AS d ON d.ClasificacionKey = s.IdClasificacion;

-- 6. Las cinco referencias del modelo estrella deben estar activas y ser confiables.
SELECT name AS Restriccion, OBJECT_NAME(referenced_object_id) AS Dimension,
       is_disabled AS Deshabilitada, is_not_trusted AS NoConfiable
FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID(N'dbo.FactOpinion')
ORDER BY Dimension;

-- Muestras de las tablas cargadas.
SELECT TOP (5) * FROM dbo.DimFecha ORDER BY Fecha;
SELECT TOP (5) ClienteKey, IdClienteOrigen FROM dbo.DimCliente ORDER BY ClienteKey;
SELECT TOP (5) * FROM dbo.DimProducto ORDER BY ProductoKey;
SELECT * FROM dbo.DimCanal ORDER BY CanalKey;
SELECT * FROM dbo.DimClasificacion ORDER BY ClasificacionKey;
GO
