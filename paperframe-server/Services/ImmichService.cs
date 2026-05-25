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
        var frameAlbumId = frameAlbum.Id;

        var album = await (apiUrl + $"albums/{frameAlbumId}")
            .WithHeader("x-api-key", config.ApiKey)
            .GetJsonAsync<ImmichAlbum>();

        var assets = album.Assets ?? Array.Empty<ImmichAsset>();
        if (assets.Length == 0)
        {
            throw new InvalidOperationException($"Album '{config.AlbumName}' contains no assets.");
        }

        var alreadyServed = getAlreadyServed(deviceId);
        string imageId;

        lock (_servedLock)
        {
            imageId = assets.Where(a => !alreadyServed.Contains(a.Id)).Shuffle().FirstOrDefault()?.Id;

            if (imageId is null)
            {
                alreadyServed.Clear();
                imageId = assets.Shuffle().First().Id;
            }

            if (imageId is not null)
                alreadyServed.Add(imageId);
        }
        
        var imageBytes = await (apiUrl + $"assets/{imageId}/thumbnail?size=preview")
            .WithHeader("x-api-key", config.ApiKey)
            .GetBytesAsync();
        
        return _imageProcessingService.ResizeCropAndAdjust(imageBytes, x, y, config.Brightness, config.Contrast);
    }
    
    private class ImmichAsset
    {
        public string Id { get; set; }
    }
    
    private class ImmichAlbum
    {
        public string AlbumName { get; set; }
        public string Id { get; set; }
        public ImmichAsset[] Assets { get; set; }
    }
}
