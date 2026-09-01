using paperframe_server.Services;

namespace paperframe_server.Tests.TestSupport;

/// <summary>A durable log sink pointed at a throwaway directory.</summary>
public static class TestLog
{
    public static PaperframeLogFile Sink() => new(new LogFilePointer(
        Path.Combine(Directory.CreateTempSubdirectory("paperframe-log-").FullName, "paperframe.log")));
}
