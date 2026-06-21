using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;

namespace paperframe_server.Services;

public class ArtChicagoService : IArtChicagoService
{
    private readonly IImageProcessingService _imageProcessingService;

    public ArtChicagoService(IImageProcessingService imageProcessingService)
    {
        _imageProcessingService = imageProcessingService;
    }

    private class ArtworkImage
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ImageId { get; set; } = string.Empty;
        public byte[] RawBytes { get; set; } = Array.Empty<byte>();
    }

    private static readonly ConcurrentDictionary<string, Queue<ArtworkImage>> _artworkQueuesPerDevice = new();
    private static readonly object _lock = new();

    private async Task RefillQueue(string deviceId, AppSettings.ArtChicagoConfig config)
    {
        var query = config.Query?.Trim() ?? "";
        
        // Try up to 3 times to get a page with at least one valid image_id
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int maxPage = string.IsNullOrEmpty(query) ? 500 : 20;
            int page = Random.Shared.Next(1, maxPage + 1);

            var url = "https://api.artic.edu/api/v1/artworks/search"
                .SetQueryParam("query[term][is_public_domain]", "true")
                .SetQueryParam("limit", "10")
                .SetQueryParam("fields", "id,title,image_id,thumbnail")
                .SetQueryParam("page", page);

            if (!string.IsNullOrEmpty(query))
            {
                url = url.SetQueryParam("q", query);
            }

            try
            {
                var response = await url
                    .WithHeader("User-Agent", "PaperframeServer/1.0 (contact@paperframe.server)")
                    .WithHeader("AIC-User-Agent", "PaperframeServer (contact@paperframe.server)")
                    .GetJsonAsync<ArtChicagoSearchResponse>();

                if (response?.Data != null && response.Data.Length > 0)
                {
                    var candidates = response.Data
                        .Where(a => !string.IsNullOrEmpty(a.ImageId))
                        .Where(a =>
                        {
                            if (string.IsNullOrEmpty(config.Orientation) || 
                                config.Orientation.Equals("all", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                            if (a.Thumbnail == null || a.Thumbnail.Width <= 0 || a.Thumbnail.Height <= 0)
                            {
                                return false; // skip if we cannot verify dimensions
                            }
                            if (config.Orientation.Equals("portrait", StringComparison.OrdinalIgnoreCase))
                            {
                                return a.Thumbnail.Height > a.Thumbnail.Width;
                            }
                            if (config.Orientation.Equals("landscape", StringComparison.OrdinalIgnoreCase))
                            {
                                return a.Thumbnail.Width > a.Thumbnail.Height;
                            }
                            return true;
                        })
                        .ToList();

                    if (candidates.Count > 0)
                    {
                        var queue = _artworkQueuesPerDevice.GetOrAdd(deviceId, _ => new Queue<ArtworkImage>());
                        
                        var downloadTasks = candidates.Select(async artwork =>
                        {
                            var iiifUrl = $"https://www.artic.edu/iiif/2/{artwork.ImageId}/full/!843,843/0/default.jpg";
                            try
                            {
                                byte[] bytes = await iiifUrl
                                    .WithHeader("User-Agent", "PaperframeServer/1.0 (contact@paperframe.server)")
                                    .WithHeader("AIC-User-Agent", "PaperframeServer (contact@paperframe.server)")
                                    .GetBytesAsync();
                                
                                return new ArtworkImage
                                {
                                    Id = artwork.Id.ToString(),
                                    Title = artwork.Title,
                                    ImageId = artwork.ImageId!,
                                    RawBytes = bytes
                                };
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to pre-download image for artwork '{artwork.Title}' ({artwork.ImageId}): {ex.Message}");
                                return null;
                            }
                        });

                        var results = await Task.WhenAll(downloadTasks);
                        var validImages = results.Where(r => r != null && r!.RawBytes.Length > 0).Cast<ArtworkImage>().ToList();

                        if (validImages.Count > 0)
                        {
                            lock (_lock)
                            {
                                foreach (var art in validImages)
                                {
                                    if (!queue.Any(q => q.ImageId == art.ImageId))
                                    {
                                        queue.Enqueue(art);
                                    }
                                }
                            }
                            return; // Successfully refilled!
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refilling ArtChicago queue (attempt {attempt + 1}): {ex.Message}");
            }
        }

        throw new InvalidOperationException("Failed to retrieve and pre-download valid public domain artworks from the Art Institute of Chicago API.");
    }

    public async Task<byte[]> GetImage(AppSettings.ArtChicagoConfig config, string deviceId, uint x, uint y)
    {
        var queue = _artworkQueuesPerDevice.GetOrAdd(deviceId, _ => new Queue<ArtworkImage>());

        ArtworkImage? artwork = null;

        lock (_lock)
        {
            if (queue.Count > 0)
            {
                artwork = queue.Dequeue();
            }
        }

        // Trigger queue refill if size drops below 3
        if (queue.Count < 3)
        {
            if (artwork == null)
            {
                try
                {
                    await RefillQueue(deviceId, config);
                    lock (_lock)
                    {
                        if (queue.Count > 0)
                        {
                            artwork = queue.Dequeue();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Foreground ArtChicago queue refill failed: {ex.Message}");
                }
            }
            else
            {
                // Run background refilling so we don't block serving the current image
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RefillQueue(deviceId, config);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Background ArtChicago queue refill failed: {ex.Message}");
                    }
                });
            }
        }

        if (artwork == null)
        {
            throw new InvalidOperationException("ArtChicago queue is empty and refill failed.");
        }

        return _imageProcessingService.ResizeCropAndAdjust(artwork.RawBytes, x, y, config.Brightness, config.Contrast, config.Rotation);
    }

    private class ArtChicagoSearchResponse
    {
        public ArtChicagoArtwork[] Data { get; set; } = Array.Empty<ArtChicagoArtwork>();
    }

    private class ArtChicagoArtwork
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("image_id")]
        public string? ImageId { get; set; }

        public ArtChicagoThumbnail? Thumbnail { get; set; }
    }

    private class ArtChicagoThumbnail
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
