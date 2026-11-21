using System.Data;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Models;
using OpinionesETL.Worker.Options;
using OpinionesETL.Worker.Services;

namespace OpinionesETL.Worker.Extractors;

public sealed class DatabaseExtractor : IExtractor
{
    private readonly ExtractionOptions _options;
    private readonly IConfiguration _configuration;
    private readonly StagingFileFactory _staging;
    private readonly ILogger<DatabaseExtractor> _logger;

    public DatabaseExtractor(IOptions<ExtractionOptions> options, IConfiguration configuration,
        StagingFileFactory staging, ILogger<DatabaseExtractor> logger)
    {
        _options = options.Value;
        _configuration = configuration;
        _staging = staging;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExtractionResult>> ExtractAsync(CancellationToken cancellationToken)
    {
        if (!_options.Database.Enabled) return Array.Empty<ExtractionResult>();
        var connectionString = _configuration.GetConnectionString("SourceDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Falta ConnectionStrings__SourceDatabase. Configúrela como variable de entorno.");

        var watch = Stopwatch.StartNew();
        _logger.LogInformation("Iniciando extracción relacional {Fuente}", _options.Database.Name);
        await using var staging = _staging.Open(_options.Database.Name);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT [IdReview], [IdCliente], [IdProducto], [Fecha], [Comentario], [Rating]
            FROM [dbo].[WebReviews]
            ORDER BY [IdReview];
            """;
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess, cancellationToken);

        var count = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            var fields = new Dictionary<string, string?>
            {
                ["IdReview"] = reader.IsDBNull(0) ? null : reader.GetString(0),
                ["IdCliente"] = reader.IsDBNull(1) ? null : reader.GetString(1),
                ["IdProducto"] = reader.IsDBNull(2) ? null : reader.GetString(2),
                ["Fecha"] = reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["Comentario"] = reader.IsDBNull(4) ? null : reader.GetString(4),
                ["Rating"] = reader.IsDBNull(5) ? null : reader.GetInt32(5).ToString(CultureInfo.InvariantCulture)
            };
            await staging.WriteAsync(new ExtractedRecord(_options.Database.Name, "SQL",
                DateTimeOffset.UtcNow, fields), cancellationToken);
            count++;
        }
        var output = await staging.CompleteAsync(cancellationToken);
        watch.Stop();
        _logger.LogInformation("SQL {Fuente}: {Registros} filas en {Ms} ms. Staging: {Salida}",
            _options.Database.Name, count, watch.ElapsedMilliseconds, output);
        return new[] { new ExtractionResult(_options.Database.Name, count, output, watch.ElapsedMilliseconds) };
    }
}
