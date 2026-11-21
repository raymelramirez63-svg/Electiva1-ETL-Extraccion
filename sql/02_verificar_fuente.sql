USE [OpinionesFuentesDB];
GO
SELECT COUNT(*) AS TotalReviews FROM dbo.WebReviews;
SELECT TOP (10) IdReview, IdCliente, IdProducto, Fecha, Rating, Comentario
FROM dbo.WebReviews ORDER BY IdReview;
GO
