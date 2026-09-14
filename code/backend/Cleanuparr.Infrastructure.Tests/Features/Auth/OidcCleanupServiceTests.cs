using System.Reflection;
using Cleanuparr.Infrastructure.Features.Auth;
using Cleanuparr.Infrastructure.Features.Auth.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Auth;

[Collection(OidcStaticStateCollection.Name)]
public sealed class OidcCleanupServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_PurgesExpiredOneTimeCodesWithoutAnyLogin()
    {
        // Arrange
        FakeTimeProvider timeProvider = new(Now);
        OidcCleanupService service = new(NullLogger<OidcCleanupService>.Instance, timeProvider);
        string code = InsertOneTimeCode(Now);

        using CancellationTokenSource cts = new();

        // Act
        await service.StartAsync(cts.Token);

        // only the injected clock moves, and nothing calls StartAuthorization
        for (int attempt = 0; attempt < 200 && OneTimeCodes().ContainsKey(code); attempt++)
        {
            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await Task.Delay(10);
        }

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        OneTimeCodes().ContainsKey(code).ShouldBeFalse();
    }

    public void Dispose()
    {
        OidcStaticState.Clear();
    }

    private static IDictionary<string, object> OneTimeCodes()
    {
        object codes = typeof(OidcAuthService)
            .GetField("OneTimeCodes", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        return ((System.Collections.IEnumerable)codes)
            .Cast<object>()
            .ToDictionary(
                kvp => (string)kvp.GetType().GetProperty("Key")!.GetValue(kvp)!,
                kvp => kvp.GetType().GetProperty("Value")!.GetValue(kvp)!);
    }

    private static string InsertOneTimeCode(DateTimeOffset createdAt)
    {
        object codes = typeof(OidcAuthService)
            .GetField("OneTimeCodes", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        object entry = Activator.CreateInstance(typeof(OidcOneTimeCodeEntry))!;
        SetProperty(entry, "AccessToken", "access");
        SetProperty(entry, "RefreshToken", "refresh");
        SetProperty(entry, "ExpiresIn", 3600);
        SetProperty(entry, "CreatedAt", createdAt);

        string code = "sweep-test-" + Guid.NewGuid().ToString("N");
        codes.GetType().GetMethod("TryAdd")!.Invoke(codes, [code, entry]);
        return code;
    }

    private static void SetProperty(object target, string name, object value) =>
        target.GetType().GetProperty(name)!.SetValue(target, value);
}
