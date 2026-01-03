using Cleanuparr.Persistence.Models.Configuration.Notification;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.Notification;

public sealed class TelegramConfigTests
{
    #region IsValid Tests

    [Fact]
    public void IsValid_WithValidBotTokenAndChatId_ReturnsTrue()
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "123456789"
        };

        config.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WithNegativeChatId_ReturnsTrue()
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "-1001234567890" // Group chat ID
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullBotToken_ReturnsFalse(string? botToken)
    {
        var config = new TelegramConfig
        {
            BotToken = botToken ?? string.Empty,
            ChatId = "123456789"
        };

        config.IsValid().ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullChatId_ReturnsFalse(string? chatId)
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = chatId ?? string.Empty
        };

        config.IsValid().ShouldBeFalse();
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("abc123")]
    [InlineData("12.34")]
    public void IsValid_WithInvalidChatId_ReturnsFalse(string chatId)
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = chatId
        };

        config.IsValid().ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_WithEmptyOrNullTopicId_ReturnsTrue(string? topicId)
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "123456789",
            TopicId = topicId
        };

        config.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WithValidTopicId_ReturnsTrue()
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "123456789",
            TopicId = "42"
        };

        config.IsValid().ShouldBeTrue();
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("abc")]
    [InlineData("12.34")]
    public void IsValid_WithInvalidTopicId_ReturnsFalse(string topicId)
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "123456789",
            TopicId = topicId
        };

        config.IsValid().ShouldBeFalse();
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidConfig_DoesNotThrow()
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "123456789"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullBotToken_ThrowsValidationException(string? botToken)
    {
        var config = new TelegramConfig
        {
            BotToken = botToken ?? string.Empty,
            ChatId = "123456789"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Telegram bot token is required");
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("short")]
    [InlineData("a")]
    public void Validate_WithBotTokenTooShort_ThrowsValidationException(string botToken)
    {
        var config = new TelegramConfig
        {
            BotToken = botToken,
            ChatId = "123456789"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Telegram bot token must be at least 10 characters long");
    }

    [Fact]
    public void Validate_WithBotTokenAtMinimumLength_DoesNotThrow()
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890", // Exactly 10 characters
            ChatId = "123456789"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullChatId_ThrowsValidationException(string? chatId)
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = chatId ?? string.Empty
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Telegram chat ID is required");
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("abc123")]
    [InlineData("12.34")]
    public void Validate_WithInvalidChatId_ThrowsValidationException(string chatId)
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = chatId
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Telegram chat ID must be a valid integer (negative IDs allowed for groups)");
    }

    [Fact]
    public void Validate_WithNegativeChatId_DoesNotThrow()
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "-1001234567890" // Group chat ID
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("abc")]
    [InlineData("12.34")]
    public void Validate_WithInvalidTopicId_ThrowsValidationException(string topicId)
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "123456789",
            TopicId = topicId
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("Telegram topic ID must be a valid integer when specified");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrNullTopicId_DoesNotThrow(string? topicId)
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "123456789",
            TopicId = topicId
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WithValidTopicId_DoesNotThrow()
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "123456789",
            TopicId = "42"
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region SendSilently Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsValid_WithAnySendSilentlyValue_DoesNotAffectValidity(bool sendSilently)
    {
        var config = new TelegramConfig
        {
            BotToken = "1234567890:ABCDefgh_ijklmnop",
            ChatId = "123456789",
            SendSilently = sendSilently
        };

        config.IsValid().ShouldBeTrue();
    }

    #endregion
}
