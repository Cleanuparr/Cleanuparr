using Cleanuparr.Persistence.Models.Auth;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Auth;

public sealed class OidcConfigTests
{
    #region Validate - Disabled Config

    [Fact]
    public void Validate_Disabled_WithEmptyFields_DoesNotThrow()
    {
        var config = new OidcConfig
        {
            Enabled = false,
            IssuerUrl = string.Empty,
            ClientId = string.Empty
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_Disabled_WithPopulatedFields_DoesNotThrow()
    {
        var config = new OidcConfig
        {
            Enabled = false,
            IssuerUrl = "https://auth.example.com",
            ClientId = "my-client"
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Issuer URL

    [Fact]
    public void Validate_Enabled_ValidHttpsIssuerUrl_DoesNotThrow()
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "https://auth.example.com/application/o/cleanuparr/",
            ClientId = "my-client",
            ProviderName = "Authentik"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Enabled_EmptyIssuerUrl_ThrowsValidationException(string issuerUrl)
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = issuerUrl,
            ClientId = "my-client"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("OIDC Issuer URL is required when OIDC is enabled");
    }

    [Fact]
    public void Validate_Enabled_InvalidIssuerUrl_ThrowsValidationException()
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "not-a-valid-url",
            ClientId = "my-client"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("OIDC Issuer URL must be a valid absolute URL");
    }

    [Fact]
    public void Validate_Enabled_HttpIssuerUrl_ThrowsValidationException()
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "http://auth.example.com",
            ClientId = "my-client"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("OIDC Issuer URL must use HTTPS");
    }

    [Theory]
    [InlineData("http://localhost:8080/auth")]
    [InlineData("http://127.0.0.1:9000/auth")]
    [InlineData("http://[::1]:9000/auth")]
    public void Validate_Enabled_HttpLocalhostIssuerUrl_DoesNotThrow(string issuerUrl)
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = issuerUrl,
            ClientId = "my-client",
            ProviderName = "Dev"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_Enabled_IssuerUrlWithTrailingSlash_DoesNotThrow()
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "https://auth.example.com/",
            ClientId = "my-client",
            ProviderName = "Authentik"
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Client ID

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Enabled_EmptyClientId_ThrowsValidationException(string clientId)
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "https://auth.example.com",
            ClientId = clientId
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("OIDC Client ID is required when OIDC is enabled");
    }

    #endregion

    #region Validate - Provider Name

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Enabled_EmptyProviderName_ThrowsValidationException(string providerName)
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "https://auth.example.com",
            ClientId = "my-client",
            ProviderName = providerName
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("OIDC Provider Name is required when OIDC is enabled");
    }

    #endregion

    #region Validate - Full Valid Configs

    [Fact]
    public void Validate_Enabled_ValidFullConfig_DoesNotThrow()
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "https://authentik.example.com/application/o/cleanuparr/",
            ClientId = "cleanuparr-client-id",
            ClientSecret = "my-secret",
            Scopes = "openid profile email",
            AuthorizedSubject = "user-123-abc",
            ProviderName = "Authentik"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_Enabled_WithoutClientSecret_DoesNotThrow()
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "https://auth.example.com",
            ClientId = "my-client",
            ClientSecret = string.Empty,
            ProviderName = "Keycloak"
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Default Values

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new OidcConfig();

        config.Enabled.ShouldBeFalse();
        config.IssuerUrl.ShouldBe(string.Empty);
        config.ClientId.ShouldBe(string.Empty);
        config.ClientSecret.ShouldBe(string.Empty);
        config.Scopes.ShouldBe("openid profile email");
        config.AuthorizedSubject.ShouldBe(string.Empty);
        config.ProviderName.ShouldBe("OIDC");
    }

    #endregion

    #region Additional Edge Cases

    [Fact]
    public void Validate_Enabled_HttpLocalhostWithoutPort_DoesNotThrow()
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "http://localhost/auth",
            ClientId = "my-client",
            ProviderName = "Dev"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_Enabled_ScopesWithoutOpenid_StillPasses()
    {
        // Documenting current behavior: the Validate method does not enforce "openid" in scopes.
        // This is intentional — the IdP will reject if openid is missing, giving a clear error.
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "https://auth.example.com",
            ClientId = "my-client",
            Scopes = "profile email",
            ProviderName = "Test"
        };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_Enabled_FtpScheme_ThrowsValidationException()
    {
        var config = new OidcConfig
        {
            Enabled = true,
            IssuerUrl = "ftp://auth.example.com",
            ClientId = "my-client"
        };

        var exception = Should.Throw<ValidationException>(() => config.Validate());
        exception.Message.ShouldBe("OIDC Issuer URL must use HTTPS");
    }

    #endregion
}
