using System;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace paperframe_server.Helpers;

/// <summary>Display geometry a device reported, in pixels.</summary>
public readonly record struct ScreenSize(uint Width, uint Height)
{
    /// <summary>The Kindle the layouts were authored against.</summary>
    public static readonly ScreenSize Default = new(758, 1024);

    public static ScreenSize Parse(string? value)
    {
        var parts = value?.Split(',');

        return parts is { Length: >= 2 }
            && uint.TryParse(parts[0], out var width)
            && uint.TryParse(parts[1], out var height)
            && width > 0
                ? new ScreenSize(width, height)
                : Default;
    }

    /// <summary>Factor for scaling a layout authored against <see cref="Default"/> onto this screen.</summary>
    public decimal ScaleFromReference() => Width / (decimal)Default.Width;

    public override string ToString() => $"{Width},{Height}";
}

public readonly record struct DeviceRequest(
    string DeviceId,
    int? Battery,
    string ScreenResolution,
    string ScriptVersion)
{
    /// <summary>False when the client sent no usable <c>device_id</c> header.</summary>
    public bool IsIdentified => DeviceId != DeviceRequestReader.UnknownDeviceId;

    /// <summary>Parsed <see cref="ScreenResolution"/>, falling back to <see cref="ScreenSize.Default"/>.</summary>
    public ScreenSize Screen => ScreenSize.Parse(ScreenResolution);
}

public static class DeviceRequestReader
{
    public const string UnknownDeviceId = "unknown";
    public const string DefaultScriptVersion = "unknown";

    public static readonly string DefaultScreenResolution = ScreenSize.Default.ToString();

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
