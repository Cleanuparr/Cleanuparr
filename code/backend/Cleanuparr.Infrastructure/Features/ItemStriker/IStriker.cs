using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.ItemStriker;

public interface IStriker
{
    Task<bool> StrikeAndCheckLimit(string hash, string itemName, ushort maxStrikes, StrikeType strikeType);
    Task ResetStrikeAsync(string hash, string itemName, StrikeType strikeType);
}