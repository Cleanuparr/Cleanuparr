using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public interface IInactivityFilterable
{
    /// <summary>
    /// Number of days of having no download or upload activity.
    /// </summary>
    double MaxInactiveDays { get; set; }

    void ValidateMaxInactiveDays()
    {
        if (MaxInactiveDays is < 0 and not -1)
        {
            throw new ValidationException("Max inactive days must be -1 (disabled) or a non-negative number");
        }
    }
}
