using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Features.ItemStriker;

public interface IStriker
{
    /// <summary>
    /// Strikes an item and checks if it has reached the maximum strikes limit
    /// </summary>
    /// <param name="hash">The hash of the item</param>
    /// <param name="itemName">The name of the item</param>
    /// <param name="maxStrikes">The maximum number of strikes</param>
    /// <param name="strikeType">The strike type</param>
    /// <returns>True if the limit has been reached, otherwise false</returns>
    Task<bool> StrikeAndCheckLimit(string hash, string itemName, ushort maxStrikes, StrikeType strikeType);
    Task ResetStrikeAsync(string hash, string itemName, StrikeType strikeType);
}