using OpinionesETL.Worker.Models;

namespace OpinionesETL.Worker.Extractors;

public interface IExtractor
{
    Task<IReadOnlyList<ExtractionResult>> ExtractAsync(CancellationToken cancellationToken);
}
