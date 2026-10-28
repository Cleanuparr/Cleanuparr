using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Shared.Attributes;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed partial record WebhookConfig : IConfig
{
    private static readonly string[] ReservedHeaderNames = ["Content-Type", "Content-Length"];

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required]
    public Guid NotificationConfigId { get; init; }

    [ForeignKey(nameof(NotificationConfigId))]
    public NotificationConfig NotificationConfig { get; init; } = null!;

    [Required]
    [MaxLength(1000)]
    [SensitiveData]
    public string Url { get; init; } = string.Empty;

    public WebhookMethod Method { get; init; } = WebhookMethod.Post;

    /// <summary>
    /// Extra request headers, each stored as "Name: Value".
    /// </summary>
    [SensitiveData(SensitiveDataType.HeaderValues)]
    public List<string> Headers { get; init; } = [];

    /// <summary>
    /// Splits a "Name: Value" header at the first colon and trims both parts.
    /// </summary>
    /// <returns>False when the text has no colon.</returns>
    public static bool TrySplitHeader(string header, out string name, out string value)
    {
        int separator = header.IndexOf(':');
        if (separator < 0)
        {
            name = string.Empty;
            value = string.Empty;
            return false;
        }

        name = header[..separator].Trim();
        value = header[(separator + 1)..].Trim();
        return true;
    }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Url);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            throw new CustomValidationException("Webhook URL is required");
        }

        if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new CustomValidationException("Webhook URL must be a valid HTTP or HTTPS URL");
        }

        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (string header in Headers)
        {
            if (!TrySplitHeader(header, out string name, out string value))
            {
                throw new CustomValidationException($"Webhook header '{header}' must use the format 'Name: Value'");
            }

            if (!HeaderNamePattern().IsMatch(name))
            {
                throw new CustomValidationException($"Webhook header name '{name}' is not valid");
            }

            if (ReservedHeaderNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                throw new CustomValidationException($"Webhook header '{name}' is set by Cleanuparr and cannot be overridden");
            }

            if (value.Any(char.IsControl))
            {
                throw new CustomValidationException($"Webhook header '{name}' has an invalid value");
            }

            if (!seenNames.Add(name))
            {
                throw new CustomValidationException($"Webhook header '{name}' is listed more than once");
            }
        }
    }

    [GeneratedRegex(@"^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$")]
    private static partial Regex HeaderNamePattern();
}
