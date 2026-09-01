using System.Collections.Concurrent;
using Flurl.Http;

namespace paperframe_server.Services;

public class ImmichService : IImmichService
{
    private readonly IImageProcessingService _imageProcessingService;

    public ImmichService(IImageProcessingService imageProcessingService)
    {
        _imageProcessingService = imageProcessingService;
    }

    private static readonly ConcurrentDictionary<string, List<string>> _alreadyServedImagesPerDevice = new();
    private static readonly object _servedLock = new();

    private List<string> getAlreadyServed(string deviceId)
    {
        return _alreadyServedImagesPerDevice.GetOrAdd(deviceId, _ => new List<string>());
    }
    
    public async Task<byte[]> GetImage(AppSettings.ImmichConfig config, string deviceId, uint x, uint y)
    {
        if (string.IsNullOrWhiteSpace(config.ApiUrl))
        {
            throw new ArgumentException("Immich API URL is required.", nameof(config));
        }

        var apiUrl = config.ApiUrl.TrimEnd('/') + "/";
        var albums = await (apiUrl + "albums")
            .WithHeader("x-api-key", config.ApiKey)
            .GetJsonAsync<ImmichAlbum[]>();

        var frameAlbum = albums.SingleOrDefault(a => a.AlbumName == config.AlbumName)
            ?? throw new KeyNotFoundException($"Album '{config.AlbumName}' not found in Immich. Available: {string.Join(", ", albums.Select(a => a.AlbumName))}");

        var assetIds = await GetAssetIds(apiUrl, config, frameAlbum);

        var alreadyServed = getAlreadyServed(deviceId);
        string? imageId;

        lock (_servedLock)
        {
            imageId = assetIds.Where(id => !alreadyServed.Contains(id)).Shuffle().FirstOrDefault();

            if (imageId is null)
            {
                alreadyServed.Clear();
                imageId = assetIds.Shuffle().First();
            }

            if (imageId is not null)
                alreadyServed.Add(imageId);
        }

        var imageBytes = await (apiUrl + $"assets/{imageId}/thumbnail?size=preview")
            .WithHeader("x-api-key", config.ApiKey)
            .GetBytesAsync();

        return _imageProcessingService.ResizeCropAndAdjust(imageBytes, x, y, config.Brightness, config.Contrast);
    }

    /// <summary>
    /// Immich v3 dropped the `assets` field from GET /albums/{id} — assets have to be
    /// fetched via POST /search/metadata instead. Older servers still return it directly.
    ///
    /// Rather than branching on a reported server version (brittle, and wrong the moment a
    /// server changes shape again), this trusts the album list's own assetCount as ground
    /// truth and only pays for the extra call when the primary field comes back empty
    /// despite the album not being — so it works unmodified against either server, and
    /// still reports a genuinely empty album correctly on either.
    /// </summary>
    private async Task<List<string>> GetAssetIds(string apiUrl, AppSettings.ImmichConfig config, ImmichAlbum frameAlbum)
    {
        var album = await (apiUrl + $"albums/{frameAlbum.Id}")
            .WithHeader("x-api-key", config.ApiKey)
            .GetJsonAsync<ImmichAlbum>();

        if (album.Assets.Length > 0)
        {
            return album.Assets.Select(a => a.Id).ToList();
        }

        if (frameAlbum.AssetCount == 0)
        {
            throw new InvalidOperationException($"Album '{config.AlbumName}' contains no assets.");
        }

        var search = await (apiUrl + "search/metadata")
            .WithHeader("x-api-key", config.ApiKey)
            .PostJsonAsync(new { albumIds = new[] { frameAlbum.Id }, size = frameAlbum.AssetCount })
            .ReceiveJson<ImmichSearchMetadataResponse>();

        return search.Assets.Items.Select(a => a.Id).ToList();
    }

    private class ImmichAsset
    {
        public string Id { get; set; } = string.Empty;
    }

    private class ImmichAlbum
    {
        public string AlbumName { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public ImmichAsset[] Assets { get; set; } = Array.Empty<ImmichAsset>();
        public int AssetCount { get; set; }
    }

    private class ImmichSearchMetadataResponse
    {
        public ImmichAssetPage Assets { get; set; } = new();
    }

    private class ImmichAssetPage
    {
        public ImmichAsset[] Items { get; set; } = Array.Empty<ImmichAsset>();
    }
}
