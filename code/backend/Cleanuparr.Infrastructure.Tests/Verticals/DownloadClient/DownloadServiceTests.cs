// using Data.Models.Configuration.DownloadCleaner;
// using Data.Enums;
// using Data.Models.Cache;
// using Infrastructure.Helpers;
// using Infrastructure.Verticals.Context;
// using Infrastructure.Verticals.DownloadClient;
// using NSubstitute;
// using NSubstitute.ClearExtensions;
// using Shouldly;
//
// namespace Infrastructure.Tests.Verticals.DownloadClient;
//
// public class DownloadServiceTests : IClassFixture<DownloadServiceFixture>
// {
//     private readonly DownloadServiceFixture _fixture;
//
//     public DownloadServiceTests(DownloadServiceFixture fixture)
//     {
//         _fixture = fixture;
//         _fixture.Cache.ClearSubstitute();
//         _fixture.Striker.ClearSubstitute();
//     }
//
//     public class ResetStrikesOnProgressTests : DownloadServiceTests
//     {
//         public ResetStrikesOnProgressTests(DownloadServiceFixture fixture) : base(fixture)
//         {
//         }
//
//         [Fact]
//         public void WhenStalledStrikeDisabled_ShouldNotResetStrikes()
//         {
//             // Arrange
//             TestDownloadService sut = _fixture.CreateSut(queueCleanerConfig: new()
//             {
//                 Enabled = true,
//                 RunSequentially = true,
//                 StalledResetStrikesOnProgress = false,
//             });
//
//             // Act
//             sut.ResetStalledStrikesOnProgress("test-hash", 100);
//
//             // Assert
//             _fixture.Cache.ReceivedCalls().ShouldBeEmpty();
//         }
//         
//         [Fact]
//         public void WhenProgressMade_ShouldResetStrikes()
//         {
//             // Arrange
//             const string hash = "test-hash";
//             StalledCacheItem stalledCacheItem = new StalledCacheItem { Downloaded = 100 };
//             
//             _fixture.Cache.TryGetValue(Arg.Any<object>(), out Arg.Any<object?>())
//                 .Returns(x =>
//                 {
//                     x[1] = stalledCacheItem;
//                     return true;
//                 });
//             
//             TestDownloadService sut = _fixture.CreateSut();
//
//             // Act
//             sut.ResetStalledStrikesOnProgress(hash, 200);
//
//             // Assert
//             _fixture.Cache.Received(1).Remove(CacheKeys.Strike(StrikeType.Stalled, hash));
//         }
//
//         [Fact]
//         public void WhenNoProgress_ShouldNotResetStrikes()
//         {
//             // Arrange
//             const string hash = "test-hash";
//             StalledCacheItem stalledCacheItem = new StalledCacheItem { Downloaded = 200 };
//             
//             _fixture.Cache
//                 .TryGetValue(Arg.Any<object>(), out Arg.Any<object?>())
//                 .Returns(x =>
//                 {
//                     x[1] = stalledCacheItem;
//                     return true;
//                 });
//             
//             TestDownloadService sut = _fixture.CreateSut();
//
//             // Act
//             sut.ResetStalledStrikesOnProgress(hash, 100);
//
//             // Assert
//             _fixture.Cache.DidNotReceive().Remove(Arg.Any<object>());
//         }
//     }
//
//     public class StrikeAndCheckLimitTests : DownloadServiceTests
//     {
//         public StrikeAndCheckLimitTests(DownloadServiceFixture fixture) : base(fixture)
//         {
//         }
//     }
//
//     public class ShouldCleanDownloadTests : DownloadServiceTests
//     {
//         public ShouldCleanDownloadTests(DownloadServiceFixture fixture) : base(fixture) 
//         {
//             ContextProvider.Set(ContextProvider.Keys.DownloadName, "test-download");
//         }
//
//         [Fact]
//         public void WhenRatioAndMinSeedTimeReached_ShouldReturnTrue()
//         {
//             // Arrange
//             CleanCategory category = new()
//             {
//                 Name = "test",
//                 MaxRatio = 1.0,
//                 MinSeedTime = 1,
//                 MaxSeedTime = -1
//             };
//             const double ratio = 1.5;
//             TimeSpan seedingTime = TimeSpan.FromHours(2);
//             
//             TestDownloadService sut = _fixture.CreateSut();
//
//             // Act
//             var result = sut.ShouldCleanDownload(ratio, seedingTime, category);
//
//             // Assert
//             result.ShouldSatisfyAllConditions(
//                 () => result.ShouldClean.ShouldBeTrue(),
//                 () => result.Reason.ShouldBe(CleanReason.MaxRatioReached)
//             );
//         }
//
//         [Fact]
//         public void WhenRatioReachedAndMinSeedTimeNotReached_ShouldReturnFalse()
//         {
//             // Arrange
//             CleanCategory category = new()
//             {
//                 Name = "test",
//                 MaxRatio = 1.0,
//                 MinSeedTime = 3,
//                 MaxSeedTime = -1
//             };
//             const double ratio = 1.5;
//             TimeSpan seedingTime = TimeSpan.FromHours(2);
//             
//             TestDownloadService sut = _fixture.CreateSut();
//
//             // Act
//             var result = sut.ShouldCleanDownload(ratio, seedingTime, category);
//
//             // Assert
//             result.ShouldSatisfyAllConditions(
//                 () => result.ShouldClean.ShouldBeFalse(),
//                 () => result.Reason.ShouldBe(CleanReason.None)
//             );
//         }
//
//         [Fact]
//         public void WhenMaxSeedTimeReached_ShouldReturnTrue()
//         {
//             // Arrange
//             CleanCategory category = new()
//             {
//                 Name = "test",
//                 MaxRatio = -1,
//                 MinSeedTime = 0,
//                 MaxSeedTime = 1
//             };
//             const double ratio = 0.5;
//             TimeSpan seedingTime = TimeSpan.FromHours(2);
//             
//             TestDownloadService sut = _fixture.CreateSut();
//
//             // Act
//             SeedingCheckResult result = sut.ShouldCleanDownload(ratio, seedingTime, category);
//
//             // Assert
//             result.ShouldSatisfyAllConditions(
//                 () => result.ShouldClean.ShouldBeTrue(),
//                 () => result.Reason.ShouldBe(CleanReason.MaxSeedTimeReached)
//             );
//         }
//
//         [Fact]
//         public void WhenNeitherConditionMet_ShouldReturnFalse()
//         {
//             // Arrange
//             CleanCategory category = new()
//             {
//                 Name = "test",
//                 MaxRatio = 2.0,
//                 MinSeedTime = 0,
//                 MaxSeedTime = 3
//             };
//             const double ratio = 1.0;
//             TimeSpan seedingTime = TimeSpan.FromHours(1);
//             
//             TestDownloadService sut = _fixture.CreateSut();
//
//             // Act
//             var result = sut.ShouldCleanDownload(ratio, seedingTime, category);
//
//             // Assert
//             result.ShouldSatisfyAllConditions(
//                 () => result.ShouldClean.ShouldBeFalse(),
//                 () => result.Reason.ShouldBe(CleanReason.None)
//             );
//         }
//     }
// }