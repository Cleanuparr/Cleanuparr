using Shouldly;
using Transmission.API.RPC;
using Transmission.API.RPC.Entity;
using Xunit;

namespace Cleanuparr.Infrastructure.Transmission.Tests.Contract;

/// <summary>
/// Calls every method Cleanuparr uses against a real Transmission container.
/// </summary>
public abstract class TransmissionContractTests<TFixture> : IClassFixture<TFixture>
    where TFixture : TransmissionFixture
{
    private readonly TFixture _fixture;

    protected TransmissionContractTests(TFixture fixture)
    {
        _fixture = fixture;
    }

    private Client Client => _fixture.Client;

    private void RequireDocker()
    {
        Skip.If(_fixture.UnavailableReason is not null, _fixture.UnavailableReason);
    }

    [SkippableFact]
    public async Task GetSessionInformationAsync_AfterSessionIdHandshake_ReturnsSessionWithAltSpeed()
    {
        RequireDocker();

        SessionInfo? session = await Client.GetSessionInformationAsync();

        session.ShouldNotBeNull();
        session.AlternativeSpeedEnabled.ShouldNotBeNull();
    }
}
