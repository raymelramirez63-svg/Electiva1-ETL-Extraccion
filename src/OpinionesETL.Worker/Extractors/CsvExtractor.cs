using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Models;
using OpinionesETL.Worker.Options;
using OpinionesETL.Worker.Services;

namespace OpinionesETL.Worker.Extractors;

public sealed class CsvExtractor : IExtractor
{
    private readonly ExtractionOptions _options;
    private readonly StagingFileFactory _staging;
    private readonly ILogger<CsvExtractor> _logger;

    public CsvExtractor(IOptions<ExtractionOptions> options, StagingFileFactory staging,
        ILogger<CsvExtractor> logger)
    {
        _options = options.Value;
        _staging = staging;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExtractionResult>> ExtractAsync(CancellationToken cancellationToken)
    {
        var results = new List<ExtractionResult>();
        foreach (var source in _options.CsvSources.Where(s => s.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var watch = Stopwatch.StartNew();
            var path = PathResolver.Resolve(source.Path);
            _logger.LogInformation("Iniciando CSV {Fuente}: {Ruta}", source.Name, path);

            await using var staging = _staging.Open(source.Name);
            using var reader = new StreamReader(path, new UTF8Encoding(false, true),
                detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            if (!await csv.ReadAsync())
                throw new InvalidDataException($"CSV sin cabecera: {path}");
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? throw new InvalidDataException("Cabecera no encontrada");
            if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace) ||
                headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
                throw new InvalidDataException($"Cabeceras vacías o repetidas en {path}");

            var count = 0;
            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fields = new Dictionary<string, string?>(StringComparer.Ordinal);
                for (int i = 0; i < headers.Length; i++)
                    fields.Add(headers[i], csv.GetField(i));

                await staging.WriteAsync(new ExtractedRecord(source.Name, "CSV",
                    DateTimeOffset.UtcNow, fields), cancellationToken);
                count++;
            }
            var output = await staging.CompleteAsync(cancellationToken);
            watch.Stop();
            _logger.LogInformation("CSV {Fuente}: {Registros} filas en {Ms} ms. Staging: {Salida}",
                source.Name, count, watch.ElapsedMilliseconds, output);
            results.Add(new ExtractionResult(source.Name, count, output, watch.ElapsedMilliseconds));
        }
        return results;
    }
}
