using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.DownloadCleaner;

public sealed class OrphanedFilesClientConfigTests
{
    #region Defaults

    [Fact]
    public void Defaults_EnabledIsFalse()
    {
        new OrphanedFilesClientConfig().Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Defaults_ScanDirectoriesIsEmpty()
    {
        new OrphanedFilesClientConfig().ScanDirectories.ShouldBeEmpty();
    }

    [Fact]
    public void Defaults_OrphanedDirectoryIsNull()
    {
        new OrphanedFilesClientConfig().OrphanedDirectory.ShouldBeNull();
    }

    [Fact]
    public void Defaults_ExcludePatternsIsEmpty()
    {
        new OrphanedFilesClientConfig().ExcludePatterns.ShouldBeEmpty();
    }

    [Fact]
    public void Defaults_MinFileAgeMinutesIsZero()
    {
        new OrphanedFilesClientConfig().MinFileAgeMinutes.ShouldBe(0);
    }

    [Fact]
    public void Defaults_EmptyAfterXDaysIsNull()
    {
        new OrphanedFilesClientConfig().EmptyAfterXDays.ShouldBeNull();
    }

    #endregion

    #region Validate - Self-validation

    [Fact]
    public void Validate_WhenDisabled_DoesNotThrow()
    {
        var config = new OrphanedFilesClientConfig { Enabled = false, ScanDirectories = [] };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenEnabledWithNoScanDirs_ThrowsValidationException()
    {
        var config = new OrphanedFilesClientConfig { Enabled = true, ScanDirectories = [] };

        Should.Throw<ValidationException>(() => config.Validate())
            .Message.ShouldContain("scan directory");
    }

    [Fact]
    public void Validate_WhenEnabledWithScanDirs_DoesNotThrow()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads"],
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Cross-client overlap (H1)

    [Fact]
    public void Validate_ScanDirMatchesSiblingScanDir_ThrowsValidationException()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
        };

        var sibling = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_ScanDirIsSubpathOfSiblingScanDir_ThrowsValidationException()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete/movies"],
        };

        var sibling = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_SiblingScanDirIsSubpathOfScanDir_ThrowsValidationException()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads"],
        };

        var sibling = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_ScanDirMatchesSiblingOrphanedDir_ThrowsValidationException()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/orphaned"],
        };

        var sibling = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/other"],
            OrphanedDirectory = "/downloads/orphaned",
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_OrphanedDirMatchesSiblingScanDir_ThrowsValidationException()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/client1"],
            OrphanedDirectory = "/downloads/shared",
        };

        var sibling = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/shared"],
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_NonOverlappingPaths_DoesNotThrow()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/client1"],
            OrphanedDirectory = "/downloads/orphaned1",
        };

        var sibling = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/client2"],
            OrphanedDirectory = "/downloads/orphaned2",
        };

        Should.NotThrow(() => config.Validate([sibling]));
    }

    [Fact]
    public void Validate_PathsWithMixedSeparators_DetectsOverlap()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
        };

        var sibling = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["\\downloads\\complete"],
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]));
    }

    [Fact]
    public void Validate_EmptySiblingsList_DoesNotThrow()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
        };

        Should.NotThrow(() => config.Validate([]));
    }

    #endregion

    #region Validate - Cross-client download directory

    [Fact]
    public void Validate_ScanDirOverlapsOtherClientDownloadTarget_ThrowsValidationException()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/data/downloads"],
        };

        var otherClient = MakeDownloadClient("Other", "/data/downloads/movies");

        Should.Throw<ValidationException>(() => config.Validate([], [otherClient]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_OrphanedDirOverlapsOtherClientDownloadTarget_ThrowsValidationException()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/data/downloads"],
            OrphanedDirectory = "/data/other-downloads",
        };

        var otherClient = MakeDownloadClient("Other", "/data/other-downloads/movies");

        Should.Throw<ValidationException>(() => config.Validate([], [otherClient]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_NoOverlapWithOtherClientDownloadTarget_DoesNotThrow()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/data/downloads-a"],
        };

        var otherClient = MakeDownloadClient("Other", "/data/downloads-b");

        Should.NotThrow(() => config.Validate([], [otherClient]));
    }

    [Fact]
    public void Validate_OtherClientWithoutDownloadTarget_IsIgnored()
    {
        var config = new OrphanedFilesClientConfig
        {
            Enabled = true,
            ScanDirectories = ["/data/downloads"],
        };

        var otherClient = MakeDownloadClient("Other", null);

        Should.NotThrow(() => config.Validate([], [otherClient]));
    }

    private static DownloadClientConfig MakeDownloadClient(string name, string? downloadDirectoryTarget) => new()
    {
        Name = name,
        TypeName = DownloadClientTypeName.qBittorrent,
        Type = DownloadClientType.Torrent,
        DownloadDirectoryTarget = downloadDirectoryTarget,
    };

    #endregion
}
