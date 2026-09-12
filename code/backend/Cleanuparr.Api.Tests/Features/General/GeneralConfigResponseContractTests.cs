using System.Text.Json;
using Cleanuparr.Api.Features.General.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.General;

/// <summary>
/// Records today's wire shape.
/// </summary>
public class GeneralConfigResponseContractTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly GeneralConfigController _controller;

    public GeneralConfigResponseContractTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();

        _controller = new GeneralConfigController(
            Substitute.For<ILogger<GeneralConfigController>>(),
            _dataContext);
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetGeneralConfig_ReturnsTheDocumentedKeys()
    {
        IActionResult result = await _controller.GetGeneralConfig();

        ResponseContract.Keys(result).ShouldBe(
        [
            "auth",
            "connectivityCheckEnabled",
            "connectivityCheckUrls",
            "displaySupportBanner",
            "dryRun",
            "historyRetentionDays",
            "httpCertificateValidation",
            "httpMaxRetries",
            "httpSendUserAgent",
            "httpTimeout",
            "id",
            "ignoredDownloads",
            "log",
            "statusCheckEnabled",
            "strikeInactivityWindowHours",
        ]);
    }

    [Fact]
    public async Task GetGeneralConfig_ReturnsTheDocumentedLogKeys()
    {
        IActionResult result = await _controller.GetGeneralConfig();

        JsonElement log = ResponseContract.Body(result).GetProperty("log");

        ResponseContract.Keys(log).ShouldBe(
        [
            "archiveEnabled",
            "archiveRetainedCount",
            "archiveTimeLimitHours",
            "level",
            "retainedFileCount",
            "rollingSizeMB",
            "timeLimitHours",
        ]);
    }

    [Fact]
    public async Task GetGeneralConfig_ReturnsTheDocumentedAuthKeys()
    {
        IActionResult result = await _controller.GetGeneralConfig();

        JsonElement auth = ResponseContract.Body(result).GetProperty("auth");

        ResponseContract.Keys(auth).ShouldBe(
        [
            "disableAuthForLocalAddresses",
            "trustForwardedHeaders",
            "trustedNetworks",
        ]);
    }

}
