using FluentAssertions;
using ImageMagick;
using paperframe_server.Services;
using Xunit;

namespace paperframe_server.Tests;

public class ImageProcessingServiceTests
{
    [Fact]
    public void ResizeCropAndAdjust_transforms_image_dimensions_successfully()
    {
        var service = new ImageProcessingService();
        var rawBytes = CreateTestImage(100, 80);

        var result = service.ResizeCropAndAdjust(rawBytes, 50, 40, 0, 0);

        using var image = new MagickImage(result);
        image.Width.Should().Be(50);
        image.Height.Should().Be(40);
    }

    [Fact]
    public void ResizeCropAndAdjust_scales_up_smaller_image_successfully()
    {
        var service = new ImageProcessingService();
        var rawBytes = CreateTestImage(10, 8);

        var result = service.ResizeCropAndAdjust(rawBytes, 50, 40, 0, 0);

        using var image = new MagickImage(result);
        image.Width.Should().Be(50);
        image.Height.Should().Be(40);
    }

    private static byte[] CreateTestImage(uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.White, width, height);
        image.Format = MagickFormat.Jpeg;
        return image.ToByteArray();
    }
}
