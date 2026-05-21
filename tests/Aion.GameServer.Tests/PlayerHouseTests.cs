using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerHouseTests
{
	[Fact]
	public void GetGraceSeconds_UsesLastAuctionEndBeforeTwoWeekCap()
	{
		var now = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
		var house = new PlayerHouse(51, 700200, 900200, now, null, IsInactive: true);

		var graceSeconds = house.GetGraceSeconds(() => now);

		Assert.Equal(871200, graceSeconds);
	}

	[Fact]
	public void GetGraceSeconds_UsesConfiguredAuctionEndSchedule()
	{
		var acquiredTime = new DateTime(2026, 5, 19, 18, 0, 0, DateTimeKind.Local);
		var schedule = JavaCronSchedule.WeeklyOrDefault("0 30 18 ? * TUE", DayOfWeek.Sunday, 12);
		var house = new PlayerHouse(51, 700200, 900200, acquiredTime, null, IsInactive: true);

		var graceSeconds = house.GetGraceSeconds(() => acquiredTime, schedule);

		Assert.Equal(606600, graceSeconds);
	}
}
