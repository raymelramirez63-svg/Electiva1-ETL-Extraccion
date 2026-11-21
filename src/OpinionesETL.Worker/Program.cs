using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpinionesETL.Worker;
using OpinionesETL.Worker.Extractors;
using OpinionesETL.Worker.Options;
using OpinionesETL.Worker.Services;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.Configure<ExtractionOptions>(
    builder.Configuration.GetSection(ExtractionOptions.SectionName));
builder.Services.AddSingleton<StagingFileFactory>();
builder.Services.AddSingleton<IExtractor, CsvExtractor>();
builder.Services.AddSingleton<IExtractor, DatabaseExtractor>();
builder.Services.AddSingleton<IExtractor, ApiExtractor>();
builder.Services.AddHttpClient("commentsApi", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ExtractionOptions>>().Value.Api;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();
