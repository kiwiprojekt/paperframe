using FluentAssertions;
using Flurl.Http.Testing;
using ImageMagick;
using paperframe_server.Services;

namespace paperframe_server.Tests;

public class ImmichServiceTests
{
    [Fact]
    public async Task GetImage_normalizes_api_url_fetches_thumbnail_and_resizes()
    {
        using var httpTest = new HttpTest();
        var imageBytes = CreateImageBytes(80, 60);
        httpTest.RespondWithJson(new[] { new { albumName = "Frame", id = "album-1" } });
        httpTest.RespondWithJson(new
        {
            albumName = "Frame",
            id = "album-1",
            assets = new[] { new { id = "asset-1" } }
        });
        httpTest.RespondWith(() => new ByteArrayContent(imageBytes), 200);

        var service = new ImmichService(new ImageProcessingService());

        var result = await service.GetImage(new AppSettings.ImmichConfig
        {
            ApiUrl = "http://immich.local/api",
            ApiKey = "key",
            AlbumName = "Frame",
            Brightness = 5,
            Contrast = 10
        }, "kindle-a", 40, 30);

        using var processed = new MagickImage(result);
        processed.Width.Should().Be(40);
        processed.Height.Should().Be(30);
        httpTest.ShouldHaveCalled("http://immich.local/api/albums").Times(1);
        httpTest.ShouldHaveCalled("http://immich.local/api/albums/album-1").Times(1);
        httpTest.ShouldHaveCalled("http://immich.local/api/assets/asset-1/thumbnail?size=preview")
            .WithHeader("x-api-key", "key")
            .Times(1);
    }

    [Fact]
    public async Task GetImage_fails_clearly_when_album_has_no_assets()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWithJson(new[] { new { albumName = "Frame", id = "album-1" } });
        httpTest.RespondWithJson(new
        {
            albumName = "Frame",
            id = "album-1",
            assets = Array.Empty<object>()
        });

        var service = new ImmichService(new ImageProcessingService());

        var act = () => service.GetImage(new AppSettings.ImmichConfig
        {
            ApiUrl = "http://immich.local/api/",
            ApiKey = "key",
            AlbumName = "Frame"
        }, "kindle-empty", 40, 30);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Album 'Frame' contains no assets.");
        httpTest.ShouldNotHaveCalled("http://immich.local/api/assets/*");
    }

    [Fact]
    public async Task GetImage_does_not_repeat_assets_for_same_device_until_all_are_served()
    {
        using var httpTest = new HttpTest();
        var imageBytes = CreateImageBytes(40, 30);
        for (var i = 0; i < 2; i++)
        {
            httpTest.RespondWithJson(new[] { new { albumName = "Frame", id = "album-1" } });
            httpTest.RespondWithJson(new
            {
                albumName = "Frame",
                id = "album-1",
                assets = new[] { new { id = "asset-1" }, new { id = "asset-2" } }
            });
            httpTest.RespondWith(() => new ByteArrayContent(imageBytes), 200);
        }

        var service = new ImmichService(new ImageProcessingService());
        var config = new AppSettings.ImmichConfig
        {
            ApiUrl = "http://immich.local/api/",
            ApiKey = "key",
            AlbumName = "Frame"
        };

        await service.GetImage(config, "kindle-cycle", 40, 30);
        await service.GetImage(config, "kindle-cycle", 40, 30);

        var assetUrls = httpTest.CallLog
            .Select(c => c.Request.Url.ToString())
            .Where(u => u.Contains("/assets/", StringComparison.Ordinal))
            .ToList();
        assetUrls.Should().HaveCount(2);
        assetUrls.Distinct().Should().HaveCount(2);
    }

    private static byte[] CreateImageBytes(uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.White, width, height);
        image.Format = MagickFormat.Jpeg;
        return image.ToByteArray();
    }
}
