using System.Net;
using Shouldly;
using Transmission.API.RPC;
using Transmission.API.RPC.Arguments;
using Transmission.API.RPC.Entity;
using Xunit;

namespace Cleanuparr.Infrastructure.Transmission.Tests.Contract;

/// <summary>
/// Calls every method Cleanuparr uses against a real Transmission container.
/// </summary>
public abstract class TransmissionContractTests<TFixture> : IClassFixture<TFixture>
    where TFixture : TransmissionFixture
{
    private readonly TFixture _fixture;

    protected TransmissionContractTests(TFixture fixture)
    {
        _fixture = fixture;
    }

    private Client Client => _fixture.Client;

    private static readonly string[] Fields =
    [
        TorrentFields.FILES,
        TorrentFields.FILE_STATS,
        TorrentFields.HASH_STRING,
        TorrentFields.ID,
        TorrentFields.ETA,
        TorrentFields.NAME,
        TorrentFields.STATUS,
        TorrentFields.IS_PRIVATE,
        TorrentFields.DOWNLOADED_EVER,
        TorrentFields.DOWNLOAD_DIR,
        TorrentFields.SECONDS_SEEDING,
        TorrentFields.TRACKER_STATS,
        TorrentFields.UPLOAD_RATIO,
        TorrentFields.TRACKERS,
        TorrentFields.RATE_DOWNLOAD,
        TorrentFields.TOTAL_SIZE,
        TorrentFields.LABELS,
        TorrentFields.IS_FINISHED,
        TorrentFields.ADDED_DATE,
        TorrentFields.ERROR,
        TorrentFields.ERROR_STRING,
    ];

    private void RequireDocker()
    {
        Skip.If(_fixture.UnavailableReason is not null, _fixture.UnavailableReason);
    }

    private async Task<(long Id, string Hash)> StartAsync()
    {
        RequireDocker();
        return await _fixture.ResetWithSeedTorrentAsync();
    }

    private async Task<TorrentInfo> GetTorrentAsync(string hash)
    {
        TransmissionTorrents? torrents = await Client.TorrentGetAsync(Fields, hash);
        return torrents!.Torrents!.Single();
    }

    private async Task<TorrentInfo> WaitForTorrentAsync(string hash, Func<TorrentInfo, bool> condition)
    {
        TorrentInfo torrent = await GetTorrentAsync(hash);
        for (int attempt = 0; attempt < 50 && !condition(torrent); attempt++)
        {
            await Task.Delay(200);
            torrent = await GetTorrentAsync(hash);
        }

        return torrent;
    }

    [SkippableFact]
    public async Task GetSessionInformationAsync_AfterSessionIdHandshake_ReturnsSessionWithAltSpeed()
    {
        RequireDocker();

        SessionInfo? session = await Client.GetSessionInformationAsync();

        session.ShouldNotBeNull();
        session.AlternativeSpeedEnabled.ShouldNotBeNull();
#pragma warning disable CS0618
        session.DownloadDirectoryFreeSpace.ShouldNotBeNull();
#pragma warning restore CS0618
    }

    [SkippableFact]
    public async Task GetSessionInformationAsync_WrongPassword_ThrowsUnauthorized()
    {
        RequireDocker();

        HttpRequestException exception = await Should.ThrowAsync<HttpRequestException>(
            () => _fixture.CreateClient("admin", "wrong").GetSessionInformationAsync());

        exception.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task GetSessionInformationAsync_NoCredentials_ThrowsWithUnauthorizedMessage()
    {
        RequireDocker();

        Exception exception = await Should.ThrowAsync<Exception>(
            () => _fixture.CreateClient(null, null).GetSessionInformationAsync());

        (exception.Message.Contains("401") || exception.Message.Contains("Unauthorized")).ShouldBeTrue(exception.Message);
    }

    [SkippableFact]
    public async Task TorrentGetAsync_AllFields_DeserializesSeedTorrent()
    {
        (long id, string hash) = await StartAsync();

        TorrentInfo torrent = await GetTorrentAsync(hash);

        torrent.Id.ShouldBe(id);
        torrent.HashString.ShouldBe(hash);
        torrent.Name.ShouldBe("contract-seed");
        torrent.Status.ShouldBe(0);
        torrent.IsPrivate.ShouldBe(false);
        torrent.DownloadDir.ShouldNotBeNullOrEmpty();
        torrent.Files!.Length.ShouldBe(2);
        torrent.FileStats!.Length.ShouldBe(2);
        torrent.Trackers!.Single().Announce.ShouldBe("http://tracker.invalid:6969/announce");
        torrent.TrackerStats!.Length.ShouldBe(1);
        torrent.Labels.ShouldNotBeNull();
        torrent.TotalSize.ShouldBe(36000);
    }

    [SkippableFact]
    public async Task TorrentGetAsync_UnknownHash_ReturnsEmpty()
    {
        RequireDocker();

        TransmissionTorrents? torrents = await Client.TorrentGetAsync(Fields, new string('a', 40));

        (torrents?.Torrents ?? []).ShouldBeEmpty();
    }

    [SkippableFact]
    public async Task TorrentGetAsync_NoHash_ReturnsAllTorrents()
    {
        (_, string hash) = await StartAsync();

        TransmissionTorrents? torrents = await Client.TorrentGetAsync(Fields);

        torrents!.Torrents!.Single().HashString.ShouldBe(hash);
    }

    [SkippableFact]
    public async Task TorrentSetAsync_Labels_RoundTrips()
    {
        (long id, string hash) = await StartAsync();

        await Client.TorrentSetAsync(new TorrentSettings { Ids = [id], Labels = ["cleanuparr", "second"] });

        (await GetTorrentAsync(hash)).Labels.ShouldBe(["cleanuparr", "second"]);
    }

    [SkippableFact]
    public async Task TorrentSetAsync_FilesUnwanted_MarksFileNotWanted()
    {
        (long id, string hash) = await StartAsync();

        await Client.TorrentSetAsync(new TorrentSettings { Ids = [id], FilesUnwanted = [1] });

        TorrentInfo torrent = await GetTorrentAsync(hash);
        torrent.FileStats![0].Wanted.ShouldBe(true);
        torrent.FileStats![1].Wanted.ShouldBe(false);
    }

    [SkippableFact]
    public async Task TorrentSetAsync_PriorityHigh_RaisesFilePriority()
    {
        (long id, string hash) = await StartAsync();

        await Client.TorrentSetAsync(new TorrentSettings { Ids = [id], PriorityHigh = [1] });

        TorrentInfo torrent = await GetTorrentAsync(hash);
        torrent.FileStats![0].Priority.ShouldBe(0);
        torrent.FileStats![1].Priority.ShouldBe(1);
    }

    [SkippableFact]
    public async Task TorrentSetAsync_TrackerList_ReplacesTrackers()
    {
        // trackerList arrived in 4.0, 3.x silently ignores it
        Skip.If(_fixture is TransmissionFixture3);
        (long id, string hash) = await StartAsync();

        await Client.TorrentSetAsync(new TorrentSettings { Ids = [id], TrackerList = "http://one.invalid/announce\n\nhttp://two.invalid/announce" });

        TorrentInfo torrent = await GetTorrentAsync(hash);
        torrent.Trackers!.Select(tracker => tracker.Announce).ShouldBe(["http://one.invalid/announce", "http://two.invalid/announce"]);
    }

    [SkippableFact]
    public async Task TorrentSetAsync_TrackerReplace_ReplacesTrackerUrl()
    {
        (long id, string hash) = await StartAsync();
        TorrentInfo before = await GetTorrentAsync(hash);

#pragma warning disable CS0618
        await Client.TorrentSetAsync(new TorrentSettings
        {
            Ids = [id],
            TrackerReplace = [new KeyValuePair<int, string>((int)before.Trackers![0].Id, "http://replaced.invalid/announce")],
        });
#pragma warning restore CS0618

        TorrentInfo torrent = await GetTorrentAsync(hash);
        torrent.Trackers!.Single().Announce.ShouldBe("http://replaced.invalid/announce");
    }

    [SkippableFact]
    public async Task TorrentSetLocationAsync_WithoutMove_ChangesDownloadDir()
    {
        (long id, string hash) = await StartAsync();
        const string location = "/downloads/münchen & søn";

        await Client.TorrentSetLocationAsync([id], location, false);

        (await GetTorrentAsync(hash)).DownloadDir.ShouldBe(location);
    }

    [SkippableFact]
    public async Task TorrentStartAsync_StoppedTorrent_ChangesStatus()
    {
        (long id, string hash) = await StartAsync();

        await Client.TorrentStartAsync([id]);

        TorrentInfo torrent = await WaitForTorrentAsync(hash, item => item.Status != 0);
        torrent.Status.ShouldNotBe(0);
    }

    [SkippableFact]
    public async Task TorrentStopAsync_RunningTorrent_SetsStoppedStatus()
    {
        (long id, string hash) = await StartAsync();
        await Client.TorrentStartAsync([id]);
        // 3.x ignores a stop sent while the torrent still verifies
        await WaitForTorrentAsync(hash, item => item.Status >= 3);

        await Client.TorrentStopAsync([id]);

        TorrentInfo torrent = await WaitForTorrentAsync(hash, item => item.Status == 0);
        torrent.Status.ShouldBe(0);
    }

    [SkippableFact]
    public async Task TorrentRemoveAsync_DeleteData_RemovesTorrent()
    {
        (long id, string hash) = await StartAsync();

        await Client.TorrentRemoveAsync([id], true);

        TransmissionTorrents? torrents = await Client.TorrentGetAsync(Fields, hash);
        (torrents?.Torrents ?? []).ShouldBeEmpty();
    }

    [SkippableFact]
    public async Task BandwidthGroupGetAsync_ByName_ReturnsOnlyThatGroup()
    {
        RequireDocker();
        // bandwidth groups arrived in 4.0
        Skip.If(_fixture is TransmissionFixture3);
        await Client.BandwidthGroupSetAsync(new BandwidthGroupSettings { Name = "contract-group", SpeedLimitDown = 100, SpeedLimitDownEnabled = true });
        await Client.BandwidthGroupSetAsync(new BandwidthGroupSettings { Name = "other-group" });

        BandwidthGroup[]? groups = await Client.BandwidthGroupGetAsync(["contract-group"]);

        groups!.Single().Name.ShouldBe("contract-group");
    }
}
