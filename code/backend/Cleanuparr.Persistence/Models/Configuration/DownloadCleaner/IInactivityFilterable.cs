using ValidationException = Cleanuparr.Domain.Exceptions.ValidationException;

namespace Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;

public interface IInactivityFilterable
{
    double MaxInactiveDays { get; set; }

    void ValidateMaxInactiveDays()
    {
        if (MaxInactiveDays < -1)
        {
            throw new ValidationException("Max inactive days can not be less than -1");
        }
    }
}
