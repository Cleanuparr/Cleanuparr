using System.Text.Json.Serialization;

namespace Cleanuparr.Infrastructure.Features.DownloadRemover.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$target")]
[JsonDerivedType(typeof(ArrRemovalTarget), "arr")]
[JsonDerivedType(typeof(LazyLibrarianRemovalTarget), "lazyLibrarian")]
public abstract record RemovalTarget
{
    // JsonIgnore does not reach an override. Annotate each one.
    [JsonIgnore]
    public abstract string DownloadId { get; }

    [JsonIgnore]
    public abstract string Title { get; }
}
