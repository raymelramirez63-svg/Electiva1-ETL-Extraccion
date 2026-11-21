namespace OpinionesETL.Worker.Options;

public sealed class ExtractionOptions
{
    public const string SectionName = "Extraction";
    public string StagingDirectory { get; set; } = "Staging";
    public int MaxParallelSources { get; set; } = 3;
    public List<CsvSourceOptions> CsvSources { get; set; } = new();
    public DatabaseOptions Database { get; set; } = new();
    public ApiOptions Api { get; set; } = new();
}

public sealed class CsvSourceOptions
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public sealed class DatabaseOptions
{
    public string Name { get; set; } = "resenas_sql";
    public bool Enabled { get; set; } = true;
}

public sealed class ApiOptions
{
    public string Name { get; set; } = "comentarios_api";
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://localhost:5075/";
    public int PageSize { get; set; } = 100;
    public int MaxPages { get; set; } = 100;
    public int TimeoutSeconds { get; set; } = 30;
}
