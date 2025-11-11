using Azure.Storage.Blobs;

public sealed class AzureBlobStorageService : IStorageService
{
    private readonly BlobServiceClient _svc;
    private readonly IWebHostEnvironment _env;

    public AzureBlobStorageService(IConfiguration cfg, IWebHostEnvironment env)
    {
        _env = env;
        var conn = cfg["Storage:Azure:ConnectionString"];
        if (string.IsNullOrWhiteSpace(conn)) throw new InvalidOperationException("Missing Storage:Azure:ConnectionString");
        _svc = new BlobServiceClient(conn);
    }

    public async Task<string> GetLocalPathAsync(string uri, CancellationToken ct = default)
    {
        // Expect format: azblob://<container>/<blob path>
        if (!uri.StartsWith("azblob://", StringComparison.OrdinalIgnoreCase))
        {
            // fall back to local service semantics
            var local = Path.Combine(_env.ContentRootPath, "wwwroot", "pdfs", uri.Replace("blob://pdfs/", ""));
            return local;
        }

        var withoutScheme = uri.Substring("azblob://".Length);
        var slash = withoutScheme.IndexOf('/')
                    switch { < 0 => throw new InvalidOperationException("Invalid azblob URI: missing container/blob"), var i => i };
        var container = withoutScheme.Substring(0, slash);
        var blobPath = withoutScheme.Substring(slash + 1);

        var tmpDir = Path.Combine(_env.ContentRootPath, "tmp");
        Directory.CreateDirectory(tmpDir);
        var localPath = Path.Combine(tmpDir, Path.GetFileName(blobPath));

        var client = _svc.GetBlobContainerClient(container).GetBlobClient(blobPath);
        await using var fs = System.IO.File.Open(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await client.DownloadToAsync(fs, ct);
        return localPath;
    }
}

