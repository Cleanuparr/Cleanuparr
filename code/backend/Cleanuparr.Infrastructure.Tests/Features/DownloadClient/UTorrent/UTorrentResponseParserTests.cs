using Cleanuparr.Domain.Exceptions;
using Cleanuparr.Infrastructure.Features.DownloadClient.UTorrent;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.UTorrent;

public class UTorrentResponseParserTests
{
    private readonly UTorrentResponseParser _parser;

    public UTorrentResponseParserTests()
    {
        var logger = Substitute.For<ILogger<UTorrentResponseParser>>();
        _parser = new UTorrentResponseParser(logger);
    }

    #region ParseTorrentList

    [Fact]
    public void ParseTorrentList_FullRow_PopulatesAllFields()
    {
        // Arrange — 27 fields per the spec; using realistic-looking values
        const string json = """
        {
            "build": 45816,
            "torrents": [
                ["HASH123", 137, "Ubuntu.iso", 1024000, 1000, 1024000, 0, 1000, 0, 0, -1, "linux",
                 5, 50, 5, 50, 1, 0, 0, "", "", "", "stream-id", 1700000000, 1700001000, "", "/downloads"]
            ],
            "label": [["linux", 1]]
        }
        """;

        // Act
        var response = _parser.ParseTorrentList(json);

        // Assert
        response.Build.ShouldBe(45816);
        response.Torrents.Count.ShouldBe(1);
        var torrent = response.Torrents[0];
        torrent.Hash.ShouldBe("HASH123");
        torrent.Status.ShouldBe(137);
        torrent.Name.ShouldBe("Ubuntu.iso");
        torrent.Size.ShouldBe(1024000);
        torrent.Progress.ShouldBe(1000);
        torrent.SavePath.ShouldBe("/downloads");
        response.Labels.ShouldContain("linux");
    }

    [Fact]
    public void ParseTorrentList_NumericFieldsAsStrings_StillParsed()
    {
        // Arrange — some µTorrent builds send numeric fields as quoted strings
        const string json = """
        {
            "build": 45816,
            "torrents": [
                ["HASH123", "137", "Ubuntu.iso", "1024000", "1000", "1024000", "0", "1000", "0", "0", "-1", "linux",
                 "5", "50", "5", "50", "1", "0", "0", "", "", "", "stream-id", "1700000000", "1700001000", "", "/downloads"]
            ],
            "label": []
        }
        """;

        // Act
        var response = _parser.ParseTorrentList(json);

        // Assert
        var torrent = response.Torrents.ShouldHaveSingleItem();
        torrent.Status.ShouldBe(137);
        torrent.Size.ShouldBe(1024000);
        torrent.Progress.ShouldBe(1000);
        torrent.ETA.ShouldBe(-1);
        torrent.DateAdded.ShouldBe(1700000000);
    }

    [Fact]
    public void ParseTorrentList_EmptyTorrentsAndLabels_ReturnsEmptyLists()
    {
        // Arrange
        const string json = """{"build": 1, "torrents": [], "label": []}""";

        // Act
        var response = _parser.ParseTorrentList(json);

        // Assert
        response.Torrents.ShouldBeEmpty();
        response.Labels.ShouldBeEmpty();
    }

    [Fact]
    public void ParseTorrentList_RowShorterThan27Fields_SkipsRow()
    {
        // Arrange — only 5 fields per torrent
        const string json = """{"build": 1, "torrents": [["HASH", 0, "name", 100, 1000]], "label": []}""";

        // Act
        var response = _parser.ParseTorrentList(json);

        // Assert — short rows are silently skipped
        response.Torrents.ShouldBeEmpty();
    }

    [Fact]
    public void ParseTorrentList_LabelWithEmptyName_Skipped()
    {
        // Arrange
        const string json = """{"build": 1, "torrents": [], "label": [["", 0], ["good", 1]]}""";

        // Act
        var response = _parser.ParseTorrentList(json);

        // Assert
        response.Labels.ShouldHaveSingleItem().ShouldBe("good");
    }

    [Fact]
    public void ParseTorrentList_MalformedJson_ThrowsUTorrentParsingException()
    {
        // Arrange
        const string json = "{ not valid json";

        // Act / Assert
        Should.Throw<UTorrentParsingException>(() => _parser.ParseTorrentList(json));
    }

    [Fact]
    public void ParseTorrentList_NullJsonBody_ThrowsUTorrentParsingException()
    {
        // Arrange
        const string json = "null";

        // Act / Assert
        Should.Throw<UTorrentParsingException>(() => _parser.ParseTorrentList(json));
    }

    #endregion

    #region ParseFileList

    [Fact]
    public void ParseFileList_FullRow_PopulatesHashAndFiles()
    {
        // Arrange — files[0] is hash, files[1] is JArray of arrays
        const string json = """
        {
            "files": ["HASH123", [["movie.mkv", 1000, 500, 0], ["sub.srt", 100, 100, 1]]]
        }
        """;

        // Act
        var response = _parser.ParseFileList(json);

        // Assert
        response.Hash.ShouldBe("HASH123");
        response.Files.Count.ShouldBe(2);
        response.Files[0].Name.ShouldBe("movie.mkv");
        response.Files[0].Size.ShouldBe(1000);
        response.Files[0].Downloaded.ShouldBe(500);
        response.Files[0].Priority.ShouldBe(0);
        response.Files[1].Name.ShouldBe("sub.srt");
        response.Files[1].Priority.ShouldBe(1);
    }

    [Fact]
    public void ParseFileList_NumericFieldsAsStrings_StillParsed()
    {
        // Arrange
        const string json = """{"files": ["HASH123", [["movie.mkv", "1000", "500", "0"]]]}""";

        // Act
        var response = _parser.ParseFileList(json);

        // Assert
        var file = response.Files.ShouldHaveSingleItem();
        file.Size.ShouldBe(1000);
        file.Downloaded.ShouldBe(500);
        file.Priority.ShouldBe(0);
    }

    [Fact]
    public void ParseFileList_EmptyFilesArray_HashSetButFilesEmpty()
    {
        // Arrange
        const string json = """{"files": ["HASH", []]}""";

        // Act
        var response = _parser.ParseFileList(json);

        // Assert
        response.Hash.ShouldBe("HASH");
        response.Files.ShouldBeEmpty();
    }

    [Fact]
    public void ParseFileList_FileRowShorterThan4Fields_Skipped()
    {
        // Arrange
        const string json = """{"files": ["HASH", [["partial", 100]]]}""";

        // Act
        var response = _parser.ParseFileList(json);

        // Assert
        response.Files.ShouldBeEmpty();
    }

    [Fact]
    public void ParseFileList_MalformedJson_ThrowsUTorrentParsingException()
    {
        // Arrange
        const string json = "{ broken";

        // Act / Assert
        Should.Throw<UTorrentParsingException>(() => _parser.ParseFileList(json));
    }

    [Fact]
    public void ParseFileList_NullBody_ThrowsUTorrentParsingException()
    {
        // Arrange
        const string json = "null";

        // Act / Assert
        Should.Throw<UTorrentParsingException>(() => _parser.ParseFileList(json));
    }

    #endregion

    #region ParseProperties

    [Fact]
    public void ParseProperties_PrivateTorrent_DetectsPexNegativeOne()
    {
        // Arrange — property names match the C# properties; deserialization is case-insensitive.
        const string json = """
        {
            "props": [{
                "Hash": "HASH",
                "Trackers": "http://tracker.example/announce",
                "Pex": -1,
                "SeedRatio": 1500
            }]
        }
        """;

        // Act
        var response = _parser.ParseProperties(json);

        // Assert
        response.Properties.ShouldNotBeNull();
        response.Properties!.Hash.ShouldBe("HASH");
        response.Properties.IsPrivate.ShouldBeTrue();
        response.Properties.SeedRatio.ShouldBe(1500);
        response.Properties.SeedRatioValue.ShouldBe(1.5);
    }

    [Fact]
    public void ParseProperties_EmptyPropsArray_ReturnsDefaultProperties()
    {
        // Arrange
        const string json = """{"props": []}""";

        // Act
        var response = _parser.ParseProperties(json);

        // Assert — no parsing happens; Properties stays at the empty default
        response.Properties.ShouldNotBeNull();
        response.Properties!.Hash.ShouldBe(string.Empty);
        response.Properties.IsPrivate.ShouldBeFalse();
    }

    [Fact]
    public void ParseProperties_TrackersWithCrLf_SplitsIntoList()
    {
        // Arrange
        const string json = "{\"props\": [{\"Hash\": \"H\", \"Trackers\": \"http://a/announce\\r\\nhttp://b/announce\", \"Pex\": 1}]}";

        // Act
        var response = _parser.ParseProperties(json);

        // Assert
        response.Properties!.TrackerList.Count.ShouldBe(2);
        response.Properties.TrackerList.ShouldContain("http://a/announce");
        response.Properties.TrackerList.ShouldContain("http://b/announce");
    }

    [Fact]
    public void ParseProperties_MalformedJson_ThrowsUTorrentParsingException()
    {
        Should.Throw<UTorrentParsingException>(() => _parser.ParseProperties("{ broken"));
    }

    [Fact]
    public void ParseProperties_NullBody_ThrowsUTorrentParsingException()
    {
        Should.Throw<UTorrentParsingException>(() => _parser.ParseProperties("null"));
    }

    #endregion

    #region ParseLabelList

    [Fact]
    public void ParseLabelList_MixedLabels_PopulatesNonEmpty()
    {
        // Arrange
        const string json = """{"label": [["movies", 3], ["", 0], ["tv", 5]]}""";

        // Act
        var response = _parser.ParseLabelList(json);

        // Assert
        response.Labels.Count.ShouldBe(2);
        response.Labels.ShouldContain("movies");
        response.Labels.ShouldContain("tv");
    }

    [Fact]
    public void ParseLabelList_EmptyLabelArray_ReturnsEmptyList()
    {
        // Arrange
        const string json = """{"label": []}""";

        // Act
        var response = _parser.ParseLabelList(json);

        // Assert
        response.Labels.ShouldBeEmpty();
    }

    [Fact]
    public void ParseLabelList_MalformedJson_ThrowsUTorrentParsingException()
    {
        Should.Throw<UTorrentParsingException>(() => _parser.ParseLabelList("{ broken"));
    }

    [Fact]
    public void ParseLabelList_NullBody_ThrowsUTorrentParsingException()
    {
        Should.Throw<UTorrentParsingException>(() => _parser.ParseLabelList("null"));
    }

    #endregion
}
