using Microsoft.Extensions.Options;

namespace paperframe_server.Tests.TestSupport;

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    public TestOptionsMonitor(T currentValue)
    {
        CurrentValue = currentValue;
    }

    public T CurrentValue { get; set; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
