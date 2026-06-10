using FluentAssertions;
using Flurl.Http.Testing;
using ImageMagick;
using paperframe_server.Services;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Flurl.Http;
using System.Net;

namespace paperframe_server.Tests;

public class ArtChicagoServiceTests
{
    [Fact]
    public async Task TestRealDownload()
    {
        var url = "https://www.artic.edu/iiif/2/91c51644-871f-cda9-82bb-94f4973ae339/full/!843,843/0/default.jpg";
        
        try
        {
            var req = url
                .WithHeader("AIC-User-Agent", "PaperframeServer (contact@paperframe.server)");

            var bytes = await req.GetBytesAsync();
            bytes.Should().NotBeNullOrEmpty();
            Console.WriteLine($"SUCCESS: Downloaded {bytes.Length} bytes using AIC-User-Agent.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetImage_refills_queue_on_first_load_and_downloads_iiif_image()
    {
        using var httpTest = new HttpTest();
        var rawImageBytes = CreateImageBytes(100, 80);

        // First mock response: artwork search results
        httpTest.RespondWithJson(new
        {
            data = new[]
            {
                new { id = 12345, title = "A Cat", image_id = "cat-image-uuid", thumbnail = new { width = 100, height = 80 } }
            }
        });

        // Second mock response: IIIF image bytes download
        httpTest.RespondWith(() => new ByteArrayContent(rawImageBytes), 200);

        // Third mock response (background queue refill): artwork search results (triggered by queue falling below 3)
        httpTest.RespondWithJson(new
        {
            data = new[]
            {
                new { id = 54321, title = "Another Cat", image_id = "cat-image-uuid-2", thumbnail = new { width = 100, height = 80 } }
            }
        });

        var service = new ArtChicagoService(new ImageProcessingService());

        var result = await service.GetImage(new AppSettings.ArtChicagoConfig
        {
            Query = "cats",
            FbinkPath = "/mnt/us/libkh/bin/fbink",
            Brightness = 10,
            Contrast = 20
        }, "kindle-a", 50, 40);

        // Assert size of processed output
        using var processed = new MagickImage(result);
        processed.Width.Should().Be(50);
        processed.Height.Should().Be(40);

        // Assert HTTP endpoints were queried correctly
        var calls = httpTest.CallLog.ToList();
        calls.Should().HaveCountGreaterThanOrEqualTo(2);
        
        var searchUrl = calls[0].Request.Url.ToString();
        searchUrl.Should().Contain("api.artic.edu/api/v1/artworks/search");
        searchUrl.Should().Contain("query[term][is_public_domain]=true");
        searchUrl.Should().Contain("q=cats");

        var imageUrl = calls[1].Request.Url.ToString();
        imageUrl.Should().Be("https://www.artic.edu/iiif/2/cat-image-uuid/full/!843,843/0/default.jpg");
    }

    [Fact]
    public async Task GetImage_filters_by_orientation_portrait()
    {
        using var httpTest = new HttpTest();
        var rawImageBytes = CreateImageBytes(100, 80);

        // Mock response: first page has one landscape (filtered out) and one portrait
        httpTest.RespondWithJson(new
        {
            data = new[]
            {
                new { id = 111, title = "Landscape Cat", image_id = "landscape-uuid", thumbnail = new { width = 200, height = 100 } },
                new { id = 222, title = "Portrait Cat", image_id = "portrait-uuid", thumbnail = new { width = 100, height = 200 } }
            }
        });

        httpTest.RespondWith(() => new ByteArrayContent(rawImageBytes), 200);

        // Background refill mock
        httpTest.RespondWithJson(new
        {
            data = new[]
            {
                new { id = 333, title = "Another Portrait Cat", image_id = "portrait-uuid-2", thumbnail = new { width = 100, height = 200 } }
            }
        });

        var service = new ArtChicagoService(new ImageProcessingService());

        var result = await service.GetImage(new AppSettings.ArtChicagoConfig
        {
            Query = "cats",
            Orientation = "portrait",
            FbinkPath = "/mnt/us/libkh/bin/fbink",
            Brightness = 10,
            Contrast = 20
        }, "kindle-b", 50, 40);

        var calls = httpTest.CallLog.ToList();
        calls.Should().HaveCountGreaterThanOrEqualTo(2);
        
        var imageUrl = calls[1].Request.Url.ToString();
        imageUrl.Should().Be("https://www.artic.edu/iiif/2/portrait-uuid/full/!843,843/0/default.jpg");
    }

    private static byte[] CreateImageBytes(uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.White, width, height);
        image.Format = MagickFormat.Jpeg;
        return image.ToByteArray();
    }
}
