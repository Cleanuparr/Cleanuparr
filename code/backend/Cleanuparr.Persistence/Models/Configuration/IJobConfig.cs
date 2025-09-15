namespace Cleanuparr.Persistence.Models.Configuration;

public interface IJobConfig : IConfig
{
    bool Enabled { get; set; }
    
    string CronExpression { get; set; }
}