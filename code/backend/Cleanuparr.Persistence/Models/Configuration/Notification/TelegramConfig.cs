using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Attributes;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.Notification;

public sealed record TelegramConfig : IConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required]
    [ExcludeFromCodeCoverage]
    public Guid NotificationConfigId { get; init; }

    public NotificationConfig NotificationConfig { get; init; } = null!;

    [Required]
    [MaxLength(255)]
    [SensitiveData]
    public string BotToken { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ChatId { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? TopicId { get; init; }

    public bool SendSilently { get; init; }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(BotToken)
               && !string.IsNullOrWhiteSpace(ChatId)
               && IsChatIdValid(ChatId)
               && IsTopicValid(TopicId);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BotToken))
        {
            throw new ValidationException("Telegram bot token is required");
        }

        if (BotToken.Length < 10)
        {
            throw new ValidationException("Telegram bot token must be at least 10 characters long");
        }

        if (string.IsNullOrWhiteSpace(ChatId))
        {
            throw new ValidationException("Telegram chat ID is required");
        }

        if (!IsChatIdValid(ChatId))
        {
            throw new ValidationException("Telegram chat ID must be a valid integer (negative IDs allowed for groups)");
        }

        if (!IsTopicValid(TopicId))
        {
            throw new ValidationException("Telegram topic ID must be a valid integer when specified");
        }
    }

    private static bool IsChatIdValid(string chatId)
    {
        return long.TryParse(chatId, out _);
    }

    private static bool IsTopicValid(string? topicId)
    {
        if (string.IsNullOrWhiteSpace(topicId))
        {
            return true;
        }

        return int.TryParse(topicId, out _);
    }
}
