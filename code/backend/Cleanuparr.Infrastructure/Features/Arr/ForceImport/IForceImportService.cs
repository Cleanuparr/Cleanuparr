using Cleanuparr.Domain.Entities.Arr.Queue;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Persistence.Models.Configuration.Arr;

namespace Cleanuparr.Infrastructure.Features.Arr.ForceImport;

public interface IForceImportService
{
    /// <summary>
    /// Imports a download an arr blocked for a reason that is safe to force past.
    /// </summary>
    Task<ForceImportOutcome> TryImportAsync(IArrClient arrClient, ArrInstance instance, QueueRecord record);
}
