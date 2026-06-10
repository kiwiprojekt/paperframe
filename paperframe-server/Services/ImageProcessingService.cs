using ImageMagick;

namespace paperframe_server.Services;

public class ImageProcessingService : IImageProcessingService
{
    public byte[] ResizeCropAndAdjust(byte[] rawImageBytes, uint width, uint height, int brightness, int contrast, int rotation = 0)
    {
        using var magickImage = new MagickImage(rawImageBytes);

        if (rotation != 0)
        {
            magickImage.Rotate(rotation);
        }

        var g = new MagickGeometry(width, height)
        {
            FillArea = true
        };
        magickImage.Resize(g);
        magickImage.Crop(width, height, Gravity.Center);
        
        magickImage.BrightnessContrast(new Percentage(brightness), new Percentage(contrast));

        return magickImage.ToByteArray();
    }
}
