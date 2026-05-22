using Cleanuparr.Persistence.Models.Configuration.OrphanedFilesCleaner;
using Shouldly;
using Xunit;
using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Tests.Models.Configuration.OrphanedFilesCleaner;

public sealed class OrphanedFilesClientConfigTests
{
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
}
