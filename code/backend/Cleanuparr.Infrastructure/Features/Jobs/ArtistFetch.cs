using Cleanuparr.Domain.Entities.Arr;

namespace Cleanuparr.Infrastructure.Features.Jobs;

internal sealed record ArtistFetch(SearchableArtist Artist, List<SearchableAlbum> Albums, List<ArrTrackFile> TrackFiles, bool Failed);
