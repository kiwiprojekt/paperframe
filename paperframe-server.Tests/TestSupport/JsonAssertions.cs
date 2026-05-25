using System.Text.Json;

namespace paperframe_server.Tests.TestSupport;

internal static class JsonAssertions
{
    public static JsonElement ToJsonElement(object value)
    {
        return JsonSerializer.SerializeToElement(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
