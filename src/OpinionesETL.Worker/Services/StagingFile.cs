using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker.Models;
using OpinionesETL.Worker.Options;

namespace OpinionesETL.Worker.Services;

public sealed class StagingFileFactory
{
    private readonly ExtractionOptions _options;

    public StagingFileFactory(IOptions<ExtractionOptions> options)
    {
        _options = options.Value;
    }

    public StagingFile Open(string source)
    {
        var directory = PathResolver.Resolve(_options.StagingDirectory);
        Directory.CreateDirectory(directory);
        var safeName = Regex.Replace(source, "[^a-zA-Z0-9_-]", "_");
        if (string.IsNullOrWhiteSpace(safeName))
            throw new ArgumentException("Nombre de fuente inválido", nameof(source));

        // Cada ejecución tiene su propio archivo. No sobreescribe ejecuciones previas.
        var unique = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}";
        var finalPath = Path.Combine(directory, $"{safeName}_{unique}.jsonl");
        return new StagingFile(finalPath);
    }
}

public sealed class StagingFile : IAsyncDisposable
{
    private readonly string _finalPath;
    private readonly string _temporaryPath;
    private readonly StreamWriter _writer;
    private bool _completed;
    private bool _disposed;

    internal StagingFile(string finalPath)
    {
        _finalPath = finalPath;
        _temporaryPath = finalPath + ".tmp";
        var stream = new FileStream(_temporaryPath, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous);
        _writer = new StreamWriter(stream, new UTF8Encoding(false), 64 * 1024);
    }

    public async Task WriteAsync(ExtractedRecord record, CancellationToken token)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StagingFile));
        var line = JsonSerializer.Serialize(record);
        await _writer.WriteLineAsync(line.AsMemory(), token);
    }

    public async Task<string> CompleteAsync(CancellationToken token)
    {
        if (_completed) throw new InvalidOperationException("Staging ya finalizado");
        await _writer.FlushAsync(token);
        await _writer.DisposeAsync();
        _disposed = true;
        File.Move(_temporaryPath, _finalPath);
        _completed = true;
        return _finalPath;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _writer.DisposeAsync();
            _disposed = true;
        }
        if (!_completed && File.Exists(_temporaryPath))
            File.Delete(_temporaryPath);
    }
}
