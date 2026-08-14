using System;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace paperframe_server.Helpers;

public readonly record struct DeviceRequest(
    string DeviceId,
    int? Battery,
    string ScreenResolution,
    string ScriptVersion)
{
    /// <summary>False when the client sent no usable <c>device_id</c> header.</summary>
    public bool IsIdentified => DeviceId != DeviceRequestReader.UnknownDeviceId;
}

public static class DeviceRequestReader
{
    public const string UnknownDeviceId = "unknown";
    public const string DefaultScreenResolution = "758,1024";
    public const string DefaultScriptVersion = "unknown";

    public const string MissingDeviceIdMessage =
        "Missing 'device_id' header. Make sure the Paperframe client sends a valid device identifier.";

    public static DeviceRequest Read(IHeaderDictionary headers)
    {
        var deviceId = headers["device_id"].FirstOrDefault();
        var battery = headers["battery"].FirstOrDefault();
        var screenRes = headers["screen_res"].FirstOrDefault();
        var scriptVersion = headers["script_version"].FirstOrDefault();

        return new DeviceRequest(
            DeviceId: string.IsNullOrEmpty(deviceId) ? UnknownDeviceId : deviceId,
            Battery: int.TryParse(battery, out var b) ? b : null,
            ScreenResolution: string.IsNullOrEmpty(screenRes) ? DefaultScreenResolution : screenRes,
            ScriptVersion: string.IsNullOrEmpty(scriptVersion) ? DefaultScriptVersion : scriptVersion);
    }
}
