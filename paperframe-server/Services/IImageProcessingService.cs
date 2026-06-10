namespace paperframe_server.Services;

public interface IImageProcessingService
{
    byte[] ResizeCropAndAdjust(byte[] rawImageBytes, uint width, uint height, int brightness, int contrast, int rotation = 0);
}
