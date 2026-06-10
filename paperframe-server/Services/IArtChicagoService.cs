namespace paperframe_server.Services;

public interface IArtChicagoService
{
    Task<byte[]> GetImage(AppSettings.ArtChicagoConfig config, string deviceId, uint x, uint y);
}
