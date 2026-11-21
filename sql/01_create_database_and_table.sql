-- Ejecutar con SQL Server Management Studio (SSMS).
-- Base auxiliar independiente para no modificar AnalisisOpinionesDB.
IF DB_ID(N'OpinionesFuentesDB') IS NULL
    CREATE DATABASE [OpinionesFuentesDB];
GO
USE [OpinionesFuentesDB];
GO
IF OBJECT_ID(N'dbo.WebReviews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WebReviews
    (
        IdReview NVARCHAR(20) NOT NULL CONSTRAINT PK_WebReviews PRIMARY KEY,
        IdCliente NVARCHAR(30) NOT NULL,
        IdProducto NVARCHAR(30) NOT NULL,
        Fecha DATE NOT NULL,
        Comentario NVARCHAR(2000) NOT NULL,
        Rating INT NOT NULL,
        CONSTRAINT CK_WebReviews_Rating CHECK (Rating BETWEEN 1 AND 5)
    );
END;
GO
SELECT COUNT(*) AS TotalReviews FROM dbo.WebReviews;
GO
