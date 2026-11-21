namespace OpinionesETL.Worker.Services;

public static class PathResolver
{
    public static string Resolve(string path) => Path.GetFullPath(
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path));
}
