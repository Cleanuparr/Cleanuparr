using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.MalwareBlocker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleanuparr.Infrastructure.Tests.Features.DownloadClient.TestHelpers;

/// <summary>
/// Test implementation of BlocklistProvider for testing purposes
/// </summary>
public static class TestBlocklistProviderFactory
{
    public static BlocklistProvider Create()
    {
        var logger = new Mock<ILogger<BlocklistProvider>>().Object;
        var scopeFactory = new Mock<IServiceScopeFactory>().Object;
        var cache = new MemoryCache(new MemoryCacheOptions());

        return new BlocklistProvider(logger, scopeFactory, cache);
    }
}
