using System.Globalization;
using CsvHelper;
using Microsoft.Data.SqlClient;

// Utilidad auxiliar para importar el CSV de reseñas a una fuente SQL real.
// Se ejecuta antes del Worker y es idempotente (no duplica IdReview).
var cs = Environment.GetEnvironmentVariable("ConnectionStrings__SourceDatabase");
if (string.IsNullOrWhiteSpace(cs))
{
    Console.Error.WriteLine("Defina ConnectionStrings__SourceDatabase antes de ejecutar el seeder.");
    Environment.ExitCode = 1;
    return;
}

var csvPath = Path.Combine(AppContext.BaseDirectory, "Data", "web_reviews.csv");
using var textReader = new StreamReader(csvPath);
using var csv = new CsvReader(textReader, CultureInfo.InvariantCulture);
var rows = csv.GetRecords<WebReview>().ToList();

await using var connection = new SqlConnection(cs);
await connection.OpenAsync();
await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
try
{
    const string sql = """
        IF NOT EXISTS (SELECT 1 FROM dbo.WebReviews WHERE IdReview = @IdReview)
        BEGIN
            INSERT INTO dbo.WebReviews
            (IdReview, IdCliente, IdProducto, Fecha, Comentario, Rating)
            VALUES (@IdReview, @IdCliente, @IdProducto, @Fecha, @Comentario, @Rating)
        END
        """;
    await using var command = new SqlCommand(sql, connection, transaction);
    command.Parameters.Add("@IdReview", System.Data.SqlDbType.NVarChar, 20);
    command.Parameters.Add("@IdCliente", System.Data.SqlDbType.NVarChar, 30);
    command.Parameters.Add("@IdProducto", System.Data.SqlDbType.NVarChar, 30);
    command.Parameters.Add("@Fecha", System.Data.SqlDbType.Date);
    command.Parameters.Add("@Comentario", System.Data.SqlDbType.NVarChar, 2000);
    command.Parameters.Add("@Rating", System.Data.SqlDbType.Int);
    foreach (var row in rows)
    {
        command.Parameters["@IdReview"].Value = row.IdReview;
        command.Parameters["@IdCliente"].Value = row.IdCliente;
        command.Parameters["@IdProducto"].Value = row.IdProducto;
        command.Parameters["@Fecha"].Value = DateTime.ParseExact(row.Fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        command.Parameters["@Comentario"].Value = row.Comentario;
        command.Parameters["@Rating"].Value = row.Rating;
        await command.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
    Console.WriteLine($"Seeder finalizado. {rows.Count} reseñas del CSV revisadas para SQL Server.");
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

public sealed class WebReview
{
    public string IdReview { get; set; } = "";
    public string IdCliente { get; set; } = "";
    public string IdProducto { get; set; } = "";
    public string Fecha { get; set; } = "";
    public string Comentario { get; set; } = "";
    public int Rating { get; set; }
}
