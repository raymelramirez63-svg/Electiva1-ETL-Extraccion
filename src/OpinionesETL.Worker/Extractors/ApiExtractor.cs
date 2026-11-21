using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Models;
using OpinionesETL.Worker.Options;
using OpinionesETL.Worker.Services;

namespace OpinionesETL.Worker.Extractors;

public sealed class ApiExtractor : IExtractor
{
    private readonly ExtractionOptions _options;
    private readonly IHttpClientFactory _httpClients;
    private readonly StagingFileFactory _staging;
    private readonly ILogger<ApiExtractor> _logger;

    public ApiExtractor(IOptions<ExtractionOptions> options, IHttpClientFactory httpClients,
        StagingFileFactory staging, ILogger<ApiExtractor> logger)
    {
        _options = options.Value;
        _httpClients = httpClients;
        _staging = staging;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExtractionResult>> ExtractAsync(CancellationToken cancellationToken)
    {
        if (!_options.Api.Enabled) return Array.Empty<ExtractionResult>();
        if (_options.Api.PageSize < 1 || _options.Api.MaxPages < 1)
            throw new InvalidOperationException("PageSize y MaxPages deben ser mayores que cero.");

        var watch = Stopwatch.StartNew();
        var client = _httpClients.CreateClient("commentsApi");
        await using var staging = _staging.Open(_options.Api.Name);
        var total = 0;
        var finished = false;
        _logger.LogInformation("Iniciando extracción API {Fuente}", _options.Api.Name);

        for (int page = 1; page <= _options.Api.MaxPages; page++)
        {
            using var response = await client.GetAsync(
                $"api/comments?page={page}&pageSize={_options.Api.PageSize}",
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<CommentsPage>(
                cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("Respuesta API nula");

            if (data.Items.Count == 0 && data.HasMore)
                throw new InvalidDataException("La API reportó más páginas pero entregó una página vacía.");
            foreach (var fields in data.Items)
            {
                await staging.WriteAsync(new ExtractedRecord(_options.Api.Name, "REST",
                    DateTimeOffset.UtcNow, fields), cancellationToken);
                total++;
            }
            if (!data.HasMore) { finished = true; break; }
        }
        if (!finished)
            throw new InvalidDataException("Se alcanzó MaxPages antes del fin de la API.");

        var output = await staging.CompleteAsync(cancellationToken);
        watch.Stop();
        _logger.LogInformation("API {Fuente}: {Registros} comentarios en {Ms} ms. Staging: {Salida}",
            _options.Api.Name, total, watch.ElapsedMilliseconds, output);
        return new[] { new ExtractionResult(_options.Api.Name, total, output, watch.ElapsedMilliseconds) };
    }

}
