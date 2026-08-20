using Cleanuparr.Domain.Entities.Arr;
using Cleanuparr.Infrastructure.Features.DownloadRemover.Models;

namespace Cleanuparr.Infrastructure.Tests.TestHelpers;

internal static class RemovalTargetExtensions
{
    internal static ArrRemovalTarget ArrTarget(this QueueItemRemoveRequest request) =>
        (ArrRemovalTarget)request.Target;

    internal static SeriesSearchItem SeriesItem(this QueueItemRemoveRequest request) =>
        (SeriesSearchItem)request.ArrTarget().SearchItem;

    internal static BookSearchItem BookItem(this QueueItemRemoveRequest request) =>
        (BookSearchItem)request.ArrTarget().SearchItem;
}
