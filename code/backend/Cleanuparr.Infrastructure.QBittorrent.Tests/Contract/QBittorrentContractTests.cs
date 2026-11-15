using Newtonsoft.Json.Linq;
using QBittorrent.Client;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests.Contract;

/// <summary>
/// Calls every method Cleanuparr uses against a real qBittorrent container.
/// </summary>
public abstract class QBittorrentContractTests<TFixture> : IClassFixture<TFixture>
    where TFixture : QBittorrentFixture
{
    private readonly TFixture _fixture;

    protected QBittorrentContractTests(TFixture fixture)
    {
        _fixture = fixture;
    }

    private QBittorrentClient Client => _fixture.Client;

    private async Task<string> StartAsync()
    {
        Skip.If(_fixture.UnavailableReason is not null, _fixture.UnavailableReason);
        return await _fixture.ResetWithSeedTorrentAsync();
    }

    private async Task<TorrentInfo> GetTorrentAsync(string hash)
    {
        IReadOnlyList<TorrentInfo> torrents = await Client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }) ?? [];
        return torrents.Single();
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
    public async Task LoginAsync_CorrectCredentials_Succeeds()
    {
        Skip.If(_fixture.UnavailableReason is not null, _fixture.UnavailableReason);

        await Should.NotThrowAsync(() => _fixture.CreateUnauthenticatedClient().LoginAsync("admin", "adminadmin"));
    }

    [SkippableFact]
    public async Task LoginAsync_WrongPassword_Throws()
    {
        Skip.If(_fixture.UnavailableReason is not null, _fixture.UnavailableReason);

        await Should.ThrowAsync<QBittorrentClientRequestException>(
            () => _fixture.CreateUnauthenticatedClient().LoginAsync("admin", "wrong"));
    }

    [SkippableFact]
    public async Task GetApiVersionAsync_ReturnsVersion()
    {
        Skip.If(_fixture.UnavailableReason is not null, _fixture.UnavailableReason);

        ApiVersion version = await Client.GetApiVersionAsync();

        ((int)version.Major).ShouldBe(2);
    }

    [SkippableFact]
    public async Task GetTorrentListAsync_EmptyQuery_ReturnsSeedTorrent()
    {
        string hash = await StartAsync();

        IReadOnlyList<TorrentInfo> torrents = await Client.GetTorrentListAsync(new TorrentListQuery()) ?? [];

        torrents.Single().Hash.ShouldBe(hash);
    }

    [SkippableFact]
    public async Task GetTorrentListAsync_HashesQuery_FiltersByHash()
    {
        string hash = await StartAsync();

        IReadOnlyList<TorrentInfo> match = await Client.GetTorrentListAsync(new TorrentListQuery { Hashes = [hash] }) ?? [];
        IReadOnlyList<TorrentInfo> miss = await Client.GetTorrentListAsync(
            new TorrentListQuery { Hashes = ["0000000000000000000000000000000000000000"] }) ?? [];

        match.Count.ShouldBe(1);
        miss.ShouldBeEmpty();
    }

    [SkippableFact]
    public async Task GetTorrentListAsync_CompletedFilter_ExcludesIncompleteTorrent()
    {
        await StartAsync();

        IReadOnlyList<TorrentInfo> torrents = await Client.GetTorrentListAsync(
            new TorrentListQuery { Filter = TorrentListFilter.Completed }) ?? [];

        torrents.ShouldBeEmpty();
    }

    [SkippableFact]
    public async Task GetTorrentPropertiesAsync_ExposesIsPrivate()
    {
        string hash = await StartAsync();

        TorrentProperties? properties = await Client.GetTorrentPropertiesAsync(hash);

        properties.ShouldNotBeNull();
        properties.AdditionalData.ShouldContainKey("is_private");
        properties.AdditionalData["is_private"].Type.ShouldBe(JTokenType.Boolean);
    }

    [SkippableFact]
    public async Task GetTorrentContentsAsync_ReturnsFileIndexNameAndPriority()
    {
        string hash = await StartAsync();

        IReadOnlyList<TorrentContent> contents = await Client.GetTorrentContentsAsync(hash) ?? [];

        TorrentContent file = contents.Single();
        file.Index.GetValueOrDefault(-1).ShouldBe(0);
        file.Name.ShouldBe("ubuntu-16.04.4-desktop-amd64.iso");
        file.Priority.ShouldBe(TorrentContentPriority.Minimal);
    }

    [SkippableFact]
    public async Task GetTorrentTrackersAsync_ReturnsAnnounceUrl()
    {
        string hash = await StartAsync();

        IReadOnlyList<TorrentTracker> trackers = await Client.GetTorrentTrackersAsync(hash) ?? [];

        trackers.ShouldContain(tracker => tracker.Url.StartsWith("http"));
    }

    [SkippableFact]
    public async Task Categories_AddAndAssign_AreVisible()
    {
        string hash = await StartAsync();

        await Client.AddCategoryAsync("contract-category");
        await Client.SetTorrentCategoryAsync([hash], "contract-category");

        IReadOnlyDictionary<string, Category> categories = await Client.GetCategoriesAsync();
        categories.Values.ShouldContain(category => category.Name == "contract-category");
        (await GetTorrentAsync(hash)).Category.ShouldBe("contract-category");
    }

    [SkippableFact]
    public async Task PauseAsync_ThenResumeAsync_ChangesState()
    {
        string hash = await StartAsync();
        bool IsStopped(TorrentInfo torrent) => torrent.State is TorrentState.PausedDownload or TorrentState.PausedUpload;
        bool IsRunning(TorrentInfo torrent) => !IsStopped(torrent);

        await Client.ResumeAsync([hash]);
        TorrentInfo resumed = await WaitForTorrentAsync(hash, IsRunning);
        IsRunning(resumed).ShouldBeTrue(resumed.State.ToString());

        await Client.PauseAsync([hash]);
        TorrentInfo paused = await WaitForTorrentAsync(hash, IsStopped);
        IsStopped(paused).ShouldBeTrue(paused.State.ToString());
    }

    [SkippableFact]
    public async Task AddTorrentTagAsync_TagAppearsOnTorrent()
    {
        string hash = await StartAsync();

        await Client.AddTorrentTagAsync([hash], "contract-tag");

        (await GetTorrentAsync(hash)).Tags.ShouldContain("contract-tag");
    }

    [SkippableFact]
    public async Task SetFilePriorityAsync_Skip_ReadsBackAsZero()
    {
        string hash = await StartAsync();

        await Client.SetFilePriorityAsync(hash, 0, TorrentContentPriority.Skip);

        IReadOnlyList<TorrentContent> contents = await Client.GetTorrentContentsAsync(hash) ?? [];
        contents.Single().Priority.ShouldBe(TorrentContentPriority.Skip);
    }

    [SkippableFact]
    public async Task SetPreferencesAsync_ExcludedFileNames_ReadsBack()
    {
        Skip.If(_fixture.UnavailableReason is not null, _fixture.UnavailableReason);
        Preferences preferences = new()
        {
            AdditionalData = new Dictionary<string, JToken> { { "excluded_file_names", "*.exe\n*.lnk" } }
        };

        await Client.SetPreferencesAsync(preferences);

        Preferences stored = await Client.GetPreferencesAsync();
        stored.AdditionalData["excluded_file_names"].ToString().ShouldBe("*.exe\n*.lnk");
    }

    [SkippableFact]
    public async Task GetAlternativeSpeedLimitsEnabledAsync_DefaultsToFalse()
    {
        Skip.If(_fixture.UnavailableReason is not null, _fixture.UnavailableReason);

        (await Client.GetAlternativeSpeedLimitsEnabledAsync()).ShouldBeFalse();
    }

    [SkippableFact]
    public async Task DeleteAsync_RemovesTorrent()
    {
        string hash = await StartAsync();

        await Client.DeleteAsync([hash], true);

        (await Client.GetTorrentListAsync() ?? []).ShouldBeEmpty();
    }
}
