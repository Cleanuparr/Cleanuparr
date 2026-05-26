using Cleanuparr.Domain.Enums;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.DownloadCleaner;

public sealed class OrphanedFilesConfigTests
{
    #region Defaults

    [Fact]
    public void Defaults_EnabledIsFalse()
    {
        new OrphanedFilesConfig().Enabled.ShouldBeFalse();
    }

    [Fact]
    public void Defaults_ScanDirectoriesIsEmpty()
    {
        new OrphanedFilesConfig().ScanDirectories.ShouldBeEmpty();
    }

    [Fact]
    public void Defaults_OrphanedDirectoryIsEmpty()
    {
        new OrphanedFilesConfig().OrphanedDirectory.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_ExcludePatternsIsEmpty()
    {
        new OrphanedFilesConfig().ExcludePatterns.ShouldBeEmpty();
    }

    [Fact]
    public void Defaults_MinFileAgeMinutesIsZero()
    {
        new OrphanedFilesConfig().MinFileAgeHours.ShouldBe(0);
    }

    [Fact]
    public void Defaults_EmptyAfterXDaysIsNull()
    {
        new OrphanedFilesConfig().EmptyAfterXDays.ShouldBeNull();
    }

    #endregion

    #region Validate - Self-validation

    [Fact]
    public void Validate_WhenDisabled_DoesNotThrow()
    {
        var config = new OrphanedFilesConfig { Enabled = false, ScanDirectories = [] };

        Should.NotThrow(() => config.Validate());
    }

    [Fact]
    public void Validate_WhenEnabledWithNoScanDirs_ThrowsValidationException()
    {
        var config = new OrphanedFilesConfig { Enabled = true, ScanDirectories = [], OrphanedDirectory = "/downloads/orphaned" };

        Should.Throw<ValidationException>(() => config.Validate())
            .Message.ShouldContain("scan directory");
    }

    [Fact]
    public void Validate_WhenEnabledWithoutOrphanedDirectory_ThrowsValidationException()
    {
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads"],
            OrphanedDirectory = string.Empty,
        };

        Should.Throw<ValidationException>(() => config.Validate())
            .Message.ShouldContain("Orphaned directory");
    }

    [Fact]
    public void Validate_WhenEnabledWithScanDirsAndOrphanedDir_DoesNotThrow()
    {
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads"],
            OrphanedDirectory = "/downloads-orphaned",
        };

        Should.NotThrow(() => config.Validate());
    }

    #endregion

    #region Validate - Cross-client overlap (H1)

    [Fact]
    public void Validate_ScanDirMatchesSiblingScanDir_ThrowsValidationException()
    {
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
            OrphanedDirectory = "/downloads/orphaned-a",
        };

        var sibling = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
            OrphanedDirectory = "/downloads/orphaned-b",
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_ScanDirIsSubpathOfSiblingScanDir_ThrowsValidationException()
    {
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete/movies"],
            OrphanedDirectory = "/downloads/orphaned-a",
        };

        var sibling = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
            OrphanedDirectory = "/downloads/orphaned-b",
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_SiblingScanDirIsSubpathOfScanDir_ThrowsValidationException()
    {
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads"],
            OrphanedDirectory = "/orphaned-a",
        };

        var sibling = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
            OrphanedDirectory = "/orphaned-b",
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_ScanDirMatchesSiblingOrphanedDir_ThrowsValidationException()
    {
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/orphaned"],
            OrphanedDirectory = "/downloads/orphaned-a",
        };

        var sibling = new OrphanedFilesConfig
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
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/client1"],
            OrphanedDirectory = "/downloads/shared",
        };

        var sibling = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/shared"],
            OrphanedDirectory = "/orphaned-b",
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_NonOverlappingPaths_DoesNotThrow()
    {
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/client1"],
            OrphanedDirectory = "/downloads/orphaned1",
        };

        var sibling = new OrphanedFilesConfig
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
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
            OrphanedDirectory = "/orphaned-a",
        };

        var sibling = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["\\downloads\\complete"],
            OrphanedDirectory = "/orphaned-b",
        };

        Should.Throw<ValidationException>(() => config.Validate([sibling]));
    }

    [Fact]
    public void Validate_EmptySiblingsList_DoesNotThrow()
    {
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/downloads/complete"],
            OrphanedDirectory = "/orphaned",
        };

        Should.NotThrow(() => config.Validate([]));
    }

    #endregion

    #region Validate - Cross-client download directory

    [Fact]
    public void Validate_ScanDirOverlapsOtherClientDownloadTarget_ThrowsValidationException()
    {
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/data/downloads"],
            OrphanedDirectory = "/data/orphaned",
        };

        var otherClient = MakeDownloadClient("Other", "/data/downloads/movies");

        Should.Throw<ValidationException>(() => config.Validate([], [otherClient]))
            .Message.ShouldContain("overlap");
    }

    [Fact]
    public void Validate_OrphanedDirOverlapsOtherClientDownloadTarget_ThrowsValidationException()
    {
        var config = new OrphanedFilesConfig
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
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/data/downloads-a"],
            OrphanedDirectory = "/data/orphaned",
        };

        var otherClient = MakeDownloadClient("Other", "/data/downloads-b");

        Should.NotThrow(() => config.Validate([], [otherClient]));
    }

    [Fact]
    public void Validate_OtherClientWithoutDownloadTarget_IsIgnored()
    {
        var config = new OrphanedFilesConfig
        {
            Enabled = true,
            ScanDirectories = ["/data/downloads"],
            OrphanedDirectory = "/data/orphaned",
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
