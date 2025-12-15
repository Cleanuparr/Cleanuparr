using System.Text;
using CliWrap;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public sealed class AppriseCliDetector : IAppriseCliDetector
{
    private readonly ILogger<AppriseCliDetector> _logger;
    
    private static readonly TimeSpan DetectionTimeout = TimeSpan.FromSeconds(5);

    public AppriseCliDetector(ILogger<AppriseCliDetector> logger)
    {
        _logger = logger;
    }

    public async Task<string?> GetAppriseVersionAsync()
    {
        using var cts = new CancellationTokenSource(DetectionTimeout);

        try
        {
            StringBuilder version = new();
            _ = await Cli.Wrap("apprise")
                .WithArguments("--version")
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(version))
                .ExecuteAsync(cts.Token);

            return version.ToString().Split('\n').FirstOrDefault();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get apprise version");
            return null;
        }
    }
}
