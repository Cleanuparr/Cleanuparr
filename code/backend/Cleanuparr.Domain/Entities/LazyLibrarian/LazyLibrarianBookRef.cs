using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Domain.Entities.LazyLibrarian;

/// <summary>
/// One book the queueBook and searchBook commands accept.
/// The audio status is separate, so the library travels with the id.
/// </summary>
public sealed record LazyLibrarianBookRef
{
    public required string BookId { get; init; }

    public required BookLibrary Library { get; init; }

    public bool IsAudioBook => Library is BookLibrary.AudioBook;
}
