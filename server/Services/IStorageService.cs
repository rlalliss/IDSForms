public interface IStorageService
{
    /// Returns a local filesystem path for the given URI, downloading if needed.
    Task<string> GetLocalPathAsync(string uri, CancellationToken ct = default);
}

