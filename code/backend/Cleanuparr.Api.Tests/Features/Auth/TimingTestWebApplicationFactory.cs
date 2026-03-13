using Cleanuparr.Infrastructure.Features.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Cleanuparr.Api.Tests.Features.Auth;

/// <summary>
/// Factory variant that replaces <see cref="IPasswordService"/> with a
/// <see cref="TrackingPasswordService"/> spy so tests can assert that
/// password verification is always called regardless of username validity.
/// </summary>
public class TimingTestWebApplicationFactory : CustomWebApplicationFactory
{
    public TrackingPasswordService TrackingPasswordService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Replace IPasswordService with our tracking spy
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPasswordService));
            if (descriptor != null) services.Remove(descriptor);

            services.AddSingleton<IPasswordService>(TrackingPasswordService);
        });
    }
}
