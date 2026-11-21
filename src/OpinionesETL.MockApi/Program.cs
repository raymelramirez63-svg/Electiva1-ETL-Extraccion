using System.Globalization;
using CsvHelper;

// API local de prueba: publica comentarios reales de social_comments.csv
// por HTTP, en lugar de fingir una llamada REST leyendo el CSV directamente.
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5075");
var app = builder.Build();

var path = Path.Combine(AppContext.BaseDirectory, "Data", "social_comments.csv");
var rows = new List<Dictionary<string, string?>>();
using (var stream = new StreamReader(path))
using (var csv = new CsvReader(stream, CultureInfo.InvariantCulture))
{
    if (!csv.Read())
        throw new InvalidDataException("social_comments.csv no contiene cabecera.");
    csv.ReadHeader();
    if (csv.HeaderRecord is null)
        throw new InvalidDataException("social_comments.csv no contiene cabecera.");
    var headers = csv.HeaderRecord ?? throw new InvalidDataException("Cabecera no encontrada.");
    while (csv.Read())
    {
        var record = new Dictionary<string, string?>();
        for (int i = 0; i < headers.Length; i++)
            record[headers[i]] = csv.GetField(i);
        rows.Add(record);
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", source = "social_comments.csv" }));
app.MapGet("/api/comments", (int? page, int? pageSize) =>
{
    var pageNumber = page ?? 1;
    var size = pageSize ?? 100;
    if (pageNumber < 1 || size < 1 || size > 500)
        return Results.BadRequest(new { error = "page >= 1 y 1 <= pageSize <= 500" });

    var offset = (long)(pageNumber - 1) * size;
    var items = offset >= rows.Count
        ? new List<Dictionary<string, string?>>()
        : rows.Skip((int)offset).Take(size).ToList();
    return Results.Ok(new
    {
        items,
        hasMore = offset + items.Count < rows.Count
    });
});

app.Logger.LogInformation("API REST lista. Comentarios cargados: {Count}", rows.Count);
await app.RunAsync();
