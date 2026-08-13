using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Domain.Entities.Arr;

/// <summary>
/// The status of a command in an arr instance.
/// </summary>
public sealed record ArrCommandStatus(long Id, ArrCommandState Status, string? Message);
