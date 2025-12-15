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

        var command = BuildArguments(payload, serviceUrls);
        await ExecuteAppriseAsync(command);
    }

    internal static string BuildArguments(ApprisePayload payload, string[] urls)
    {
        var sb = new StringBuilder(" --verbose");

        if (!string.IsNullOrEmpty(payload.Title))
        {
            sb.Append($" --title=\"{EscapeArgument(payload.Title)}\"");
        }

        sb.Append($" --body=\"{EscapeArgument(payload.Body)}\"");
        sb.Append($" --notification-type={payload.Type}");

        foreach (var url in urls)
        {
            sb.Append($" \"{EscapeArgument(url)}\"");
        }

        return sb.ToString();
    }

    private static async Task ExecuteAppriseAsync(string arguments)
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

    internal static string EscapeArgument(string arg)
    {
        // Escape double quotes and backslashes for shell
        return arg.Replace("\\", @"\\").Replace("\"", "\\\"");
    }
}
