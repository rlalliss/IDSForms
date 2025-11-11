using Azure.Storage.Files.Shares;

public sealed class AzureFileShareStorageService : IStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly string? _conn;

    public AzureFileShareStorageService(IConfiguration cfg, IWebHostEnvironment env)
    {
        _env = env;
        _conn = cfg["Storage:AzureFiles:ConnectionString"]; // optional if using SAS URLs
    }

    public async Task<string> GetLocalPathAsync(string uri, CancellationToken ct = default)
    {
        // Support explicit Azure Files scheme: azfiles://<share>/<path>
        if (uri.StartsWith("azfiles://", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(_conn))
                throw new InvalidOperationException("Missing Storage:AzureFiles:ConnectionString for azfiles:// URIs");

            var without = uri.Substring("azfiles://".Length);
            var slash = without.IndexOf('/') switch { < 0 => throw new InvalidOperationException("Invalid azfiles URI: missing share/file"), var i => i };
            var share = without.Substring(0, slash);
            var filePath = without.Substring(slash + 1);
            return await DownloadAzureFileAsync(share, filePath, ct);
        }

        // Support full HTTPS Azure Files URL (optionally with SAS): https://<acct>.file.core.windows.net/<share>/<path>[?sastoken]
        if (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && uri.Contains(".file.core.windows.net/", StringComparison.OrdinalIgnoreCase))
        {
            var u = new Uri(uri);
            // If URL has SAS, we can use it directly. Otherwise, fall back to connection string if available.
            if (!string.IsNullOrEmpty(u.Query))
            {
                var clientWithSas = new ShareFileClient(u);
                return await DownloadToTempAsync(clientWithSas, ct);
            }

            if (string.IsNullOrWhiteSpace(_conn))
                throw new InvalidOperationException("Azure Files HTTPS URL provided without SAS and no Storage:AzureFiles:ConnectionString configured");

            var path = u.AbsolutePath.TrimStart('/');
            var idx = path.IndexOf('/') switch { < 0 => throw new InvalidOperationException("Invalid Azure Files URL: missing share/file"), var i => i };
            var share = path.Substring(0, idx);
            var filePath = path.Substring(idx + 1);
            var client = new ShareFileClient(_conn!, share, filePath);
            return await DownloadToTempAsync(client, ct);
        }

        // Local/file-share mapping for blob://pdfs/ URIs (compatibility with existing content)
        if (uri.StartsWith("blob://pdfs/", StringComparison.OrdinalIgnoreCase))
        {
            var local = Path.Combine(_env.ContentRootPath, "wwwroot", "pdfs", uri.Replace("blob://pdfs/", ""));
            return local;
        }

        // If it's already a filesystem path (absolute/UNC), return as-is
        if (Path.IsPathRooted(uri) || uri.StartsWith("\\\\"))
            return uri;

        // Unknown scheme; return as-is and let callers handle errors
        return uri;
    }

    private async Task<string> DownloadAzureFileAsync(string shareName, string filePath, CancellationToken ct)
    {
        var fileClient = new ShareFileClient(_conn!, shareName, filePath.Replace('\\', '/'));
        return await DownloadToTempAsync(fileClient, ct);
    }

    private async Task<string> DownloadToTempAsync(ShareFileClient fileClient, CancellationToken ct)
    {
        var tmpDir = Path.Combine(_env.ContentRootPath, "tmp");
        Directory.CreateDirectory(tmpDir);
        var localPath = Path.Combine(tmpDir, Path.GetFileName(fileClient.Path));
        await using var fs = System.IO.File.Open(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await (await fileClient.DownloadAsync(cancellationToken: ct)).Value.Content.CopyToAsync(fs, ct);
        return localPath;
    }
}
