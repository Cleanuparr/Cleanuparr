using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.Arr.Dtos;

public class ArrConfigDto
{
    public Guid Id { get; set; }
    
    public required InstanceType Type { get; set; }

    public short FailedImportMaxStrikes { get; set; } = -1;

    public List<ArrInstanceDto> Instances { get; set; } = [];
}