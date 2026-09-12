using System.Text.Json;
using Cleanuparr.Api.Features.DownloadClient.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadClient.Controllers;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Features.DownloadClient;

/// <summary>
/// Records today's wire shape, leaks included.
/// </summary>
public class DownloadClientResponseContractTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly DownloadClientController _controller;

    public DownloadClientResponseContractTests()
    {
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();

        _controller = new DownloadClientController(
            Substitute.For<ILogger<DownloadClientController>>(),
            _dataContext,
            Substitute.For<IDynamicHttpClientFactory>(),
            Substitute.For<IDownloadServiceFactory>());
        ConfigControllerTestDataFactory.ConfigureProblemDetails(_controller);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetDownloadClientConfig_ReturnsTheDocumentedWrapperKeys()
    {
        await SeedClientAsync();

        IActionResult result = await _controller.GetDownloadClientConfig();

        ResponseContract.Keys(result).ShouldBe(["clients"]);
    }

    [Fact]
    public async Task GetDownloadClientConfig_ReturnsTheDocumentedClientKeys()
    {
        await SeedClientAsync();

        IActionResult result = await _controller.GetDownloadClientConfig();

        ResponseContract.Keys(FirstClient(result)).ShouldBe(ClientKeys);
    }

    [Fact]
    public async Task CreateDownloadClientConfig_ReturnsTheDocumentedKeys()
    {
        IActionResult result = await _controller.CreateDownloadClientConfig(NewCreateRequest());

        ResponseContract.Keys(result).ShouldBe(ClientKeys);
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_ReturnsTheDocumentedKeys()
    {
        DownloadClientConfig client = await SeedClientAsync();

        IActionResult result = await _controller.UpdateDownloadClientConfig(client.Id, NewUpdateRequest());

        ResponseContract.Keys(result).ShouldBe(ClientKeys);
    }

    [Fact]
    public async Task GetDownloadClientConfig_MasksThePassword()
    {
        await SeedClientAsync();

        IActionResult result = await _controller.GetDownloadClientConfig();

        FirstClient(result).GetProperty("password").GetString().ShouldBe(SensitiveDataHelper.Placeholder);
    }

    [Fact]
    public async Task CreateDownloadClientConfig_MasksThePassword()
    {
        IActionResult result = await _controller.CreateDownloadClientConfig(NewCreateRequest());

        ResponseContract.Body(result).GetProperty("password").GetString().ShouldBe(SensitiveDataHelper.Placeholder);
    }

    [Fact]
    public async Task UpdateDownloadClientConfig_MasksThePassword()
    {
        DownloadClientConfig client = await SeedClientAsync();

        IActionResult result = await _controller.UpdateDownloadClientConfig(client.Id, NewUpdateRequest());

        ResponseContract.Body(result).GetProperty("password").GetString().ShouldBe(SensitiveDataHelper.Placeholder);
    }

    private static readonly string[] ClientKeys =
    [
        "downloadDirectorySource",
        "downloadDirectoryTarget",
        "enabled",
        "externalUrl",
        "host",
        "id",
        "name",
        "password",
        "type",
        "typeName",
        "urlBase",
        "username",
    ];

    private static JsonElement FirstClient(IActionResult result) =>
        ResponseContract.Body(result).GetProperty("clients").EnumerateArray().First();

    private async Task<DownloadClientConfig> SeedClientAsync()
    {
        DownloadClientConfig client = new()
        {
            Id = Guid.NewGuid(),
            Name = "contract",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://localhost:8080"),
            Username = "user",
            Password = "secret",
        };

        _dataContext.DownloadClients.Add(client);
        await _dataContext.SaveChangesAsync();
        return client;
    }

    private static CreateDownloadClientRequest NewCreateRequest() => new()
    {
        Enabled = true,
        Name = "created",
        TypeName = DownloadClientTypeName.qBittorrent,
        Type = DownloadClientType.Torrent,
        Host = "http://localhost:8080",
        Username = "user",
        Password = "secret",
    };

    private static UpdateDownloadClientRequest NewUpdateRequest() => new()
    {
        Enabled = true,
        Name = "renamed",
        TypeName = DownloadClientTypeName.qBittorrent,
        Type = DownloadClientType.Torrent,
        Host = "http://localhost:8080",
        Username = "user",
        Password = "secret",
    };
}
