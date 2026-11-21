namespace OpinionesETL.Worker.Models;

public sealed class CommentsPage
{
    public List<Dictionary<string, string?>> Items { get; set; } = new();
    public bool HasMore { get; set; }
}
