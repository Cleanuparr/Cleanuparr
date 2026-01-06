using System.Text;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using CliWrap;

namespace Cleanuparr.Infrastructure.Features.Notifications.Apprise;

public sealed class AppriseCliProxy : IAppriseCliProxy
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public async Task SendNotification(ApprisePayload payload, AppriseConfig config)
    {
        var serviceUrls = config.ServiceUrls?
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToArray();

        if (serviceUrls == null || serviceUrls.Length == 0)
        {
            throw new AppriseException("No service URLs configured");
        }

        var args = new List<string> { "--verbose" };

        if (!string.IsNullOrEmpty(payload.Title))
        {
            args.AddRange(["--title", payload.Title]);
        }

        args.AddRange(["--body", payload.Body, "--notification-type", payload.Type]);

        if (!string.IsNullOrEmpty(payload.ImageUrl))
        {
            args.AddRange(["--attach", payload.ImageUrl]);
        }

        args.AddRange(serviceUrls);

        await ExecuteAppriseAsync(args);
    }

    private static async Task ExecuteAppriseAsync(IEnumerable<string> arguments)
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);
        StringBuilder message = new();

        try
        {
            CommandResult result = await Cli.Wrap("apprise")
                .WithArguments(arguments)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(message))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(message))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cts.Token);

            if (!result.IsSuccess)
            {
                throw new AppriseException($"Apprise CLI failed with: {message}");
            }
        }
        catch (AppriseException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new AppriseException($"Apprise CLI timed out after {DefaultTimeout.TotalSeconds} seconds.");
        }
        catch (Exception exception)
        {
            throw new AppriseException("Apprise CLI failed", exception);
        }
    }
}
