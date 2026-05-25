using ImageMagick;

namespace paperframe_server.Services;

public class ImageProcessingService : IImageProcessingService
{
    public byte[] ResizeCropAndAdjust(byte[] rawImageBytes, uint width, uint height, int brightness, int contrast)
    {
        using var magickImage = new MagickImage(rawImageBytes);

        var g = new MagickGeometry(width, height)
        {
            FillArea = true
        };
        magickImage.Resize(g);
        magickImage.Crop(g);
        
        magickImage.BrightnessContrast(new Percentage(brightness), new Percentage(contrast));

        return magickImage.ToByteArray();
    }
}
