using FluentAssertions;
using Microsoft.AspNetCore.Http;
using paperframe_server.Helpers;

namespace paperframe_server.Tests;

public class DeviceRequestReaderTests
{
    [Fact]
    public void Read_populates_all_fields_from_headers()
    {
        var headers = new HeaderDictionary
        {
            ["device_id"] = "kindle-a",
            ["battery"] = "77",
            ["screen_res"] = "600,800",
            ["script_version"] = "1.2"
        };

        var request = DeviceRequestReader.Read(headers);

        request.DeviceId.Should().Be("kindle-a");
        request.Battery.Should().Be(77);
        request.ScreenResolution.Should().Be("600,800");
        request.ScriptVersion.Should().Be("1.2");
    }

    [Fact]
    public void Read_applies_defaults_for_missing_headers()
    {
        var request = DeviceRequestReader.Read(new HeaderDictionary());

        request.DeviceId.Should().Be(DeviceRequestReader.UnknownDeviceId);
        request.Battery.Should().BeNull();
        request.ScreenResolution.Should().Be(DeviceRequestReader.DefaultScreenResolution);
        request.ScriptVersion.Should().Be(DeviceRequestReader.DefaultScriptVersion);
    }

    [Fact]
    public void Read_treats_empty_headers_as_missing()
    {
        var headers = new HeaderDictionary
        {
            ["device_id"] = "",
            ["screen_res"] = "",
            ["script_version"] = ""
        };

        var request = DeviceRequestReader.Read(headers);

        request.DeviceId.Should().Be(DeviceRequestReader.UnknownDeviceId);
        request.ScreenResolution.Should().Be(DeviceRequestReader.DefaultScreenResolution);
        request.ScriptVersion.Should().Be(DeviceRequestReader.DefaultScriptVersion);
    }

    [Fact]
    public void Read_treats_unparseable_battery_as_null()
    {
        var headers = new HeaderDictionary
        {
            ["battery"] = "not-a-number"
        };

        var request = DeviceRequestReader.Read(headers);

        request.Battery.Should().BeNull();
    }

    [Theory]
    [InlineData("600,800", 600u, 800u)]
    [InlineData("758,1024", 758u, 1024u)]
    [InlineData("invalid", 758u, 1024u)]
    [InlineData("600", 758u, 1024u)]
    [InlineData("", 758u, 1024u)]
    public void Read_parses_the_screen_size_once_for_every_caller(string header, uint width, uint height)
    {
        var headers = new HeaderDictionary { ["device_id"] = "kindle-a" };
        if (header.Length > 0) headers["screen_res"] = header;

        var device = DeviceRequestReader.Read(headers);

        device.Screen.Should().Be(new ScreenSize(width, height));
    }

    [Fact]
    public void ScreenSize_scales_relative_to_the_reference_width()
    {
        new ScreenSize(1516, 2048).ScaleFromReference().Should().Be(2m);
        ScreenSize.Default.ScaleFromReference().Should().Be(1m);
    }
}
