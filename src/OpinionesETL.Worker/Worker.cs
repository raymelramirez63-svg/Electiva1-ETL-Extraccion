using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Extractors;
using OpinionesETL.Worker.Models;
using OpinionesETL.Worker.Options;

namespace OpinionesETL.Worker;

public sealed class Worker : BackgroundService
{
    private readonly IEnumerable<IExtractor> _extractors;
    private readonly ExtractionOptions _options;
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public Worker(IEnumerable<IExtractor> extractors, IOptions<ExtractionOptions> options,
        ILogger<Worker> logger, IHostApplicationLifetime lifetime)
    {
        _extractors = extractors;
        _options = options.Value;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var semaphore = new SemaphoreSlim(Math.Clamp(_options.MaxParallelSources, 1, 16));
        try
        {
            _logger.LogInformation("Inicio de ETL: fase de EXTRACCIÓN. Paralelismo máximo: {Max}",
                _options.MaxParallelSources);

            var tasks = _extractors.Select(async extractor =>
            {
                await semaphore.WaitAsync(stoppingToken);
                try { return await extractor.ExtractAsync(stoppingToken); }
                finally { semaphore.Release(); }
            });
            IReadOnlyList<ExtractionResult>[] groups = await Task.WhenAll(tasks);
            var results = groups.SelectMany(g => g).ToList();
            var records = results.Sum(r => r.Records);
            stopwatch.Stop();
            foreach (var result in results)
                _logger.LogInformation("RESUMEN {Fuente}: {N} registros, {Ms} ms",
                    result.Source, result.Records, result.ElapsedMilliseconds);
            _logger.LogInformation("EXTRACCIÓN COMPLETADA: {N} fuentes, {Total} registros, {Ms} ms",
                results.Count, records, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Extracción cancelada por el usuario.");
            Environment.ExitCode = 1;
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "La extracción no pudo completarse. Revisar el error y la configuración.");
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
