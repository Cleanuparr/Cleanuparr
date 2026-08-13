using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Domain.Entities.Arr;

public sealed record ArrCommandStatus(long Id, ArrCommandState Status, string? Message);
