using Cleanuparr.Api.Features.Arr.Contracts.Requests;
using Cleanuparr.Api.Features.DownloadClient.Contracts.Requests;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Shared.Helpers;
using Shouldly;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Api.Tests.Features.SensitiveData;

/// <summary>
/// Tests that placeholder values are correctly handled on the input side:
/// - UPDATE operations preserve the existing DB value when a placeholder is sent
/// - CREATE operations reject placeholder values
/// - TEST operations reject placeholder values
/// </summary>
public class SensitiveDataInputTests
{
    private const string Placeholder = SensitiveDataHelper.Placeholder;

    #region ArrInstanceRequest — UPDATE

    [Fact]
    public void ArrInstanceRequest_ApplyTo_WithPlaceholderApiKey_PreservesExistingValue()
    {
        var request = new ArrInstanceRequest
        {
            Name = "Updated Sonarr",
            Url = "http://sonarr:8989",
            ApiKey = Placeholder,
            Version = 4,
        };

        var existingInstance = new ArrInstance
        {
            Name = "Sonarr",
            Url = new Uri("http://sonarr:8989"),
            ApiKey = "original-secret-key",
            ArrConfigId = Guid.NewGuid(),
            Version = 4,
        };

        request.ApplyTo(existingInstance);

        existingInstance.ApiKey.ShouldBe("original-secret-key");
        existingInstance.Name.ShouldBe("Updated Sonarr");
    }

    [Fact]
    public void ArrInstanceRequest_ApplyTo_WithRealApiKey_UpdatesValue()
    {
        var request = new ArrInstanceRequest
        {
            Name = "Sonarr",
            Url = "http://sonarr:8989",
            ApiKey = "brand-new-api-key",
            Version = 4,
        };

        var existingInstance = new ArrInstance
        {
            Name = "Sonarr",
            Url = new Uri("http://sonarr:8989"),
            ApiKey = "original-secret-key",
            ArrConfigId = Guid.NewGuid(),
            Version = 4,
        };

        request.ApplyTo(existingInstance);

        existingInstance.ApiKey.ShouldBe("brand-new-api-key");
    }

    #endregion

    #region ArrInstanceRequest — CREATE

    [Fact]
    public void ArrInstanceRequest_ToEntity_WithPlaceholderApiKey_ThrowsValidationException()
    {
        var request = new ArrInstanceRequest
        {
            Name = "Sonarr",
            Url = "http://sonarr:8989",
            ApiKey = Placeholder,
            Version = 4,
        };

        Should.Throw<ValidationException>(() => request.ToEntity(Guid.NewGuid()));
    }

    [Fact]
    public void ArrInstanceRequest_ToEntity_WithRealApiKey_Succeeds()
    {
        var request = new ArrInstanceRequest
        {
            Name = "Sonarr",
            Url = "http://sonarr:8989",
            ApiKey = "real-api-key-123",
            Version = 4,
        };

        var entity = request.ToEntity(Guid.NewGuid());
        entity.ApiKey.ShouldBe("real-api-key-123");
    }

    #endregion

    #region TestArrInstanceRequest — TEST

    [Fact]
    public void TestArrInstanceRequest_ToTestInstance_WithPlaceholderApiKey_ThrowsValidationException()
    {
        var request = new TestArrInstanceRequest
        {
            Url = "http://sonarr:8989",
            ApiKey = Placeholder,
            Version = 4,
        };

        Should.Throw<ValidationException>(() => request.ToTestInstance());
    }

    [Fact]
    public void TestArrInstanceRequest_ToTestInstance_WithRealApiKey_Succeeds()
    {
        var request = new TestArrInstanceRequest
        {
            Url = "http://sonarr:8989",
            ApiKey = "real-api-key",
            Version = 4,
        };

        var instance = request.ToTestInstance();
        instance.ApiKey.ShouldBe("real-api-key");
    }

    #endregion

    #region UpdateDownloadClientRequest — UPDATE

    [Fact]
    public void UpdateDownloadClientRequest_ApplyTo_WithPlaceholderPassword_PreservesExistingValue()
    {
        var request = new UpdateDownloadClientRequest
        {
            Name = "Updated qBit",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = "http://qbit:8080",
            Username = "admin",
            Password = Placeholder,
        };

        var existing = new DownloadClientConfig
        {
            Name = "qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://qbit:8080"),
            Username = "admin",
            Password = "original-secret-password",
        };

        var result = request.ApplyTo(existing);

        result.Password.ShouldBe("original-secret-password");
        result.Name.ShouldBe("Updated qBit");
    }

    [Fact]
    public void UpdateDownloadClientRequest_ApplyTo_WithRealPassword_UpdatesValue()
    {
        var request = new UpdateDownloadClientRequest
        {
            Name = "qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = "http://qbit:8080",
            Username = "admin",
            Password = "new-password-123",
        };

        var existing = new DownloadClientConfig
        {
            Name = "qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = new Uri("http://qbit:8080"),
            Username = "admin",
            Password = "original-secret-password",
        };

        var result = request.ApplyTo(existing);

        result.Password.ShouldBe("new-password-123");
    }

    #endregion

    #region CreateDownloadClientRequest — CREATE

    [Fact]
    public void CreateDownloadClientRequest_Validate_WithPlaceholderPassword_ThrowsValidationException()
    {
        var request = new CreateDownloadClientRequest
        {
            Name = "qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = "http://qbit:8080",
            Password = Placeholder,
        };

        Should.Throw<ValidationException>(() => request.Validate());
    }

    [Fact]
    public void CreateDownloadClientRequest_Validate_WithRealPassword_Succeeds()
    {
        var request = new CreateDownloadClientRequest
        {
            Name = "qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = "http://qbit:8080",
            Password = "real-password",
        };

        Should.NotThrow(() => request.Validate());
    }

    [Fact]
    public void CreateDownloadClientRequest_Validate_WithNullPassword_Succeeds()
    {
        var request = new CreateDownloadClientRequest
        {
            Name = "qBittorrent",
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = "http://qbit:8080",
            Password = null,
        };

        Should.NotThrow(() => request.Validate());
    }

    #endregion

    #region TestDownloadClientRequest — TEST

    [Fact]
    public void TestDownloadClientRequest_Validate_WithPlaceholderPassword_ThrowsValidationException()
    {
        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = "http://qbit:8080",
            Password = Placeholder,
        };

        Should.Throw<ValidationException>(() => request.Validate());
    }

    [Fact]
    public void TestDownloadClientRequest_Validate_WithRealPassword_Succeeds()
    {
        var request = new TestDownloadClientRequest
        {
            TypeName = DownloadClientTypeName.qBittorrent,
            Type = DownloadClientType.Torrent,
            Host = "http://qbit:8080",
            Password = "real-password",
        };

        Should.NotThrow(() => request.Validate());
    }

    #endregion
}
