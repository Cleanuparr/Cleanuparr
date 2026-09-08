using Cleanuparr.Shared.Helpers;
using Shouldly;

namespace Cleanuparr.Api.Tests.Features;

public class LogSanitizerTests
{
    [Fact]
    public void SanitizeForLog_LeavesAPlainValueUntouched()
    {
        "admin".SanitizeForLog().ShouldBe("admin");
    }

    [Fact]
    public void SanitizeForLog_KeepsPrintableSymbolsAndAccents()
    {
        "admin.user+1_ó".SanitizeForLog().ShouldBe("admin.user+1_ó");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SanitizeForLog_MapsMissingValuesToEmpty(string? value)
    {
        value.SanitizeForLog().ShouldBe(string.Empty);
    }

    [Fact]
    public void SanitizeForLog_StripsTheLineBreaksUsedToForgeAnEntry()
    {
        string forged = "admin\n2026-01-01 00:00:00.000 [ERR] Injected entry";

        forged.SanitizeForLog().ShouldBe("admin2026-01-01 00:00:00.000 [ERR] Injected entry");
    }

    [Fact]
    public void SanitizeForLog_StripsCarriageReturns()
    {
        "admin\r\nsecond line".SanitizeForLog().ShouldBe("adminsecond line");
    }

    [Fact]
    public void SanitizeForLog_StripsEscapeAndOtherControlCharacters()
    {
        "adm\u001b[31min\tuser\0".SanitizeForLog().ShouldBe("adm[31minuser");
    }
}
