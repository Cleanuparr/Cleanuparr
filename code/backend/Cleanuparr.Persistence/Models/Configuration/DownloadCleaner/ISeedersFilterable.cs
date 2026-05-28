using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public interface ISeedersFilterable
{
    /// <summary>
    /// Minimum number of seeders required before cleanup. Set to 0 to disable.
    /// </summary>
    int MinSeeders { get; set; }

    void ValidateMinSeeders()
    {
        if (MinSeeders < 0)
        {
            throw new ValidationException("Min seeders can not be less than 0");
        }
    }
}
