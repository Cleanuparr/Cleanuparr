using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cleanuparr.Shared.Attributes;

namespace Cleanuparr.Persistence.Models.Configuration.Arr;

public sealed class ArrInstance
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public bool Enabled { get; set; }
    
    public Guid ArrConfigId { get; set; }

    public ArrConfig ArrConfig { get; set; } = null!;
    
    public required string Name { get; set; }
    
    public required Uri Url { get; set; }
    
    [SensitiveData]
    public required string ApiKey { get; set; }
}