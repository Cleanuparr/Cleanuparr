using System.Net.Security;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Services;

public class CertificateValidationServiceTests
{
    private readonly CertificateValidationService _service;

    public CertificateValidationServiceTests()
    {
        _service = new CertificateValidationService(Substitute.For<ILogger<CertificateValidationService>>());
    }

    private bool Validate(
        CertificateValidationType type,
        object sender,
        SslPolicyErrors errors
    )
    {
        return _service.ShouldByPassValidationError(type, sender, null, null, errors);
    }

    private static HttpRequestMessage Request(string url)
    {
        return new HttpRequestMessage(HttpMethod.Get, url);
    }

    [Fact]
    public void ShouldByPassValidationError_EnabledWithChainErrorsOnPublicHost_ReturnsFalse()
    {
        bool result = Validate(CertificateValidationType.Enabled, Request("https://8.8.8.8"), SslPolicyErrors.RemoteCertificateChainErrors);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ShouldByPassValidationError_EnabledWithNoErrors_ReturnsTrue()
    {
        bool result = Validate(CertificateValidationType.Enabled, Request("https://8.8.8.8"), SslPolicyErrors.None);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldByPassValidationError_DisabledWithChainErrors_ReturnsTrue()
    {
        bool result = Validate(CertificateValidationType.Disabled, Request("https://8.8.8.8"), SslPolicyErrors.RemoteCertificateChainErrors);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldByPassValidationError_DisabledForLocalAddressesOnLocalHost_ReturnsTrue()
    {
        bool result = Validate(CertificateValidationType.DisabledForLocalAddresses, Request("https://192.168.1.10"), SslPolicyErrors.RemoteCertificateChainErrors);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldByPassValidationError_DisabledForLocalAddressesOnPublicHost_ReturnsFalse()
    {
        bool result = Validate(CertificateValidationType.DisabledForLocalAddresses, Request("https://8.8.8.8"), SslPolicyErrors.RemoteCertificateChainErrors);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("https://localhost")]
    [InlineData("https://127.0.0.1")]
    public void ShouldByPassValidationError_EnabledOnLoopbackHost_ReturnsTrue(string url)
    {
        bool result = Validate(CertificateValidationType.Enabled, Request(url), SslPolicyErrors.RemoteCertificateChainErrors);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldByPassValidationError_DisabledForLocalAddressesOnUnresolvableHost_ReturnsFalse()
    {
        bool result = Validate(CertificateValidationType.DisabledForLocalAddresses, Request("https://host.invalid"), SslPolicyErrors.RemoteCertificateNameMismatch);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ShouldByPassValidationError_UnknownSenderType_ReturnsFalse()
    {
        bool result = Validate(CertificateValidationType.Enabled, new object(), SslPolicyErrors.RemoteCertificateChainErrors);

        result.ShouldBeFalse();
    }
}
