using Microsoft.Extensions.Options;

namespace paperframe_server.Tests.TestSupport;

internal sealed class TestOptionsSnapshot<T> : IOptionsSnapshot<T> where T : class
{
    public TestOptionsSnapshot(T value)
    {
        Value = value;
    }

    public T Value { get; }

    public T Get(string? name) => Value;
}
