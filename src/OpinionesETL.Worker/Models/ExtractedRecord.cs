namespace OpinionesETL.Worker.Models;

// El diccionario mantiene el valor original de cada columna sin transformar
// identificadores ni imponer relaciones entre tablas en la fase E.
public sealed record ExtractedRecord(
    string Source,
    string SourceType,
    DateTimeOffset ExtractedAtUtc,
    Dictionary<string, string?> Fields);

public sealed record ExtractionResult(
    string Source,
    int Records,
    string StagingFile,
    long ElapsedMilliseconds);
