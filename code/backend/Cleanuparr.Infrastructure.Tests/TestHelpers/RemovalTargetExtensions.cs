using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;

namespace Cleanuparr.Infrastructure.Tests.TestHelpers;

internal static class RemovalTargetExtensions
{
    internal static ArrRemovalTarget ArrTarget(this QueueItemRemoveRequest request) =>
        (ArrRemovalTarget)request.Target;

    internal static LazyLibrarianRemovalTarget LazyTarget(this QueueItemRemoveRequest request) =>
        (LazyLibrarianRemovalTarget)request.Target;

    internal static SeriesSearchItem SeriesItem(this QueueItemRemoveRequest request) =>
        (SeriesSearchItem)request.ArrTarget().SearchItem;
}
