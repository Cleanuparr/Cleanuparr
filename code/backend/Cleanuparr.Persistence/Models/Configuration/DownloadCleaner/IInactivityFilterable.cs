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
        if (MaxInactiveDays < -1)
        {
            throw new ValidationException("Max inactive days can not be less than -1");
        }
    }
}
