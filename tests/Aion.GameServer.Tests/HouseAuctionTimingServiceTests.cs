using Aion.GameServer.Configuration;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class HouseAuctionTimingServiceTests
{
	[Fact]
	public void IsBiddingTime_FollowsJavaSundayNoonCutoff()
	{
		var clock = new MutableTimeProvider(new DateTimeOffset(2026, 5, 24, 11, 59, 0, TimeSpan.Zero));
		var service = new HouseAuctionTimingService(new GameServerOptions(), clock);

		Assert.True(service.IsBiddingTime(1001));

		clock.SetUtcNow(new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero));
		Assert.False(service.IsBiddingTime(1001));

		clock.SetUtcNow(new DateTimeOffset(2026, 5, 25, 0, 0, 0, TimeSpan.Zero));
		Assert.True(service.IsBiddingTime(1001));
	}

	[Fact]
	public void TryProlongAuction_BidBeforeRegularEndExtendsHouseCountdown()
	{
		var clock = new MutableTimeProvider(new DateTimeOffset(2026, 5, 24, 11, 58, 0, TimeSpan.Zero));
		var service = new HouseAuctionTimingService(new GameServerOptions(), clock);

		Assert.True(service.TryProlongAuction(1001));

		Assert.True(service.IsBiddingTime(1001));
		Assert.Equal(420, service.GetRemainingAuctionSeconds(1001));

		clock.SetUtcNow(new DateTimeOffset(2026, 5, 24, 12, 4, 0, TimeSpan.Zero));
		Assert.True(service.IsBiddingTime(1001));
		Assert.False(service.IsBiddingTime(1002));
		Assert.Equal(60, service.GetRemainingAuctionSeconds(1001));
	}

	[Fact]
	public void GetRemainingAuctionSeconds_UsesConfiguredAuctionEndCron()
	{
		var clock = new MutableTimeProvider(new DateTimeOffset(2026, 5, 19, 18, 0, 0, TimeSpan.Zero));
		var service = new HouseAuctionTimingService(
			new GameServerOptions { Housing = new GameServerHousingOptions { AuctionEndTime = "0 30 18 ? * TUE" } },
			clock);

		Assert.Equal(1800, service.GetRemainingAuctionSeconds(1001));
		Assert.True(service.TryProlongAuction(1001));
		Assert.Equal(1800, service.GetRemainingAuctionSeconds(1001));

		clock.SetUtcNow(new DateTimeOffset(2026, 5, 19, 18, 26, 0, TimeSpan.Zero));
		Assert.True(service.TryProlongAuction(1001));
		Assert.Equal(540, service.GetRemainingAuctionSeconds(1001));
	}

	[Fact]
	public void TryProlongAuction_RepeatedBidsCapAtThirtyMinutesAfterRegularEnd()
	{
		var clock = new MutableTimeProvider(new DateTimeOffset(2026, 5, 24, 11, 58, 0, TimeSpan.Zero));
		var service = new HouseAuctionTimingService(new GameServerOptions(), clock);

		Assert.True(service.TryProlongAuction(1001));
		foreach (var minute in new[] { 4, 8, 12, 16, 20, 24, 28 })
		{
			clock.SetUtcNow(new DateTimeOffset(2026, 5, 24, 12, minute, 0, TimeSpan.Zero));
			Assert.True(service.IsBiddingTime(1001));
			Assert.True(service.TryProlongAuction(1001));
		}

		Assert.Equal(120, service.GetRemainingAuctionSeconds(1001));
	}

	[Fact]
	public void IsBiddingTime_PrunesExpiredProlongation()
	{
		var clock = new MutableTimeProvider(new DateTimeOffset(2026, 5, 24, 11, 58, 0, TimeSpan.Zero));
		var service = new HouseAuctionTimingService(new GameServerOptions(), clock);

		Assert.True(service.TryProlongAuction(1001));

		clock.SetUtcNow(new DateTimeOffset(2026, 5, 24, 12, 6, 0, TimeSpan.Zero));
		Assert.False(service.IsBiddingTime(1001));
	}

	[Fact]
	public void ShouldRunAuctionEndOnStartup_FollowsJavaRecoveryWindow()
	{
		var service = new HouseAuctionTimingService(new GameServerOptions());
		var startup = new DateTimeOffset(2026, 5, 24, 12, 45, 0, TimeSpan.Zero);

		Assert.True(service.ShouldRunAuctionEndOnStartup(
			new DateTimeOffset(2026, 5, 24, 11, 59, 0, TimeSpan.Zero),
			startup));
		Assert.True(service.ShouldRunAuctionEndOnStartup(
			new DateTimeOffset(2026, 5, 24, 12, 29, 0, TimeSpan.Zero),
			startup));
		Assert.False(service.ShouldRunAuctionEndOnStartup(
			new DateTimeOffset(2026, 5, 24, 12, 31, 0, TimeSpan.Zero),
			startup));
		Assert.False(service.ShouldRunAuctionEndOnStartup(null, startup));
	}

	private sealed class MutableTimeProvider : TimeProvider
	{
		private DateTimeOffset _utcNow;

		public MutableTimeProvider(DateTimeOffset utcNow)
		{
			_utcNow = utcNow;
		}

		public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

		public override DateTimeOffset GetUtcNow()
		{
			return _utcNow;
		}

		public void SetUtcNow(DateTimeOffset utcNow)
		{
			_utcNow = utcNow;
		}
	}
}
