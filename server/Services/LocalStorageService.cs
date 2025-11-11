public sealed class LocalStorageService : IStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly string? _rootOverride;

    public LocalStorageService(IWebHostEnvironment env, IConfiguration cfg)
    {
        _env = env;
        _rootOverride = cfg["Storage:Local:RootPath"]; // e.g., \\server\share\pdfs or C:\\pdfs
    }

    public Task<string> GetLocalPathAsync(string uri, CancellationToken ct = default)
    {
        if (uri.StartsWith("blob://pdfs/", StringComparison.OrdinalIgnoreCase))
        {
            var root = _rootOverride ?? Path.Combine(_env.ContentRootPath, "wwwroot", "pdfs");
            var relative = uri.Replace("blob://pdfs/", "");
            var local = Path.Combine(root, relative);
            return Task.FromResult(local);
        }

        // Already a path (absolute or relative)
        return Task.FromResult(uri);
    }
}
