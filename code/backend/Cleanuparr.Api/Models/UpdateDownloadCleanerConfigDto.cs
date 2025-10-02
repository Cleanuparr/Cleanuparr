using System.Diagnostics.CodeAnalysis;

using Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests;

namespace Cleanuparr.Api.Models;

/// <summary>
/// Legacy namespace shim; prefer <see cref="UpdateDownloadCleanerConfigRequest"/> from
/// <c>Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests</c>.
/// </summary>
[Obsolete("Use Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests.UpdateDownloadCleanerConfigRequest instead")]
[SuppressMessage("Design", "CA1000", Justification = "Temporary alias during refactor")]
[SuppressMessage("Usage", "CA2225", Justification = "Alias type")]
public record UpdateDownloadCleanerConfigDto : UpdateDownloadCleanerConfigRequest;

/// <summary>
/// Legacy namespace shim; prefer <see cref="CleanCategoryRequest"/> from
/// <c>Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests</c>.
/// </summary>
[Obsolete("Use Cleanuparr.Api.Features.DownloadCleaner.Contracts.Requests.CleanCategoryRequest instead")]
[SuppressMessage("Design", "CA1000", Justification = "Temporary alias during refactor")]
[SuppressMessage("Usage", "CA2225", Justification = "Alias type")]
public record CleanCategoryDto : CleanCategoryRequest;