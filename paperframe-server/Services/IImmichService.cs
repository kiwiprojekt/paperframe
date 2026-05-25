namespace paperframe_server.Services;

public interface IImmichService
{
    Task<byte[]> GetImage(AppSettings.ImmichConfig config, string deviceId, uint x, uint y);
}