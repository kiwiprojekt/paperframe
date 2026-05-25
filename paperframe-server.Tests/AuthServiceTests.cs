using FluentAssertions;
using paperframe_server.Tests.TestSupport;

namespace paperframe_server.Tests;

public class AuthServiceTests
{
    [Fact]
    public void CreateSession_returns_valid_token_until_expired()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 5, 25, 10, 0, 0, TimeSpan.Zero));
        using var service = new AuthService(clock);

        var token = service.CreateSession();

        token.Should().HaveLength(32);
        service.ValidateSession(token).Should().BeTrue();

        clock.Advance(TimeSpan.FromDays(8));

        service.ValidateSession(token).Should().BeFalse();
    }

    [Fact]
    public void ValidateSession_slides_expiration()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 5, 25, 10, 0, 0, TimeSpan.Zero));
        using var service = new AuthService(clock);
        var token = service.CreateSession();

        clock.Advance(TimeSpan.FromDays(6));
        service.ValidateSession(token).Should().BeTrue();

        clock.Advance(TimeSpan.FromDays(6));
        service.ValidateSession(token).Should().BeTrue();

        clock.Advance(TimeSpan.FromDays(8));
        service.ValidateSession(token).Should().BeFalse();
    }

    [Fact]
    public void RevokeSession_invalidates_only_that_token()
    {
        using var service = new AuthService();
        var first = service.CreateSession();
        var second = service.CreateSession();

        service.RevokeSession(first);

        service.ValidateSession(first).Should().BeFalse();
        service.ValidateSession(second).Should().BeTrue();
    }

    [Fact]
    public void RevokeAllSessions_invalidates_every_token()
    {
        using var service = new AuthService();
        var first = service.CreateSession();
        var second = service.CreateSession();

        service.RevokeAllSessions();

        service.ValidateSession(first).Should().BeFalse();
        service.ValidateSession(second).Should().BeFalse();
    }
}
