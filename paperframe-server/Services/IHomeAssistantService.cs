using System.Threading.Tasks;

namespace paperframe_server.Services;

public interface IHomeAssistantService
{
    Task UpdateEntities(string deviceId, int? battery);
}