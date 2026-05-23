using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CreaturePvpZoneCounterServiceTests
{
	[Fact]
	public void TracksNestedPvpZoneCountersWithJavaInsidePvpRule()
	{
		var service = new CreaturePvpZoneCounterService();

		var onePvp = service.EnterZone(1001, CreaturePvpZoneCounterType.Pvp);
		var twoPvp = service.EnterZone(1001, CreaturePvpZoneCounterType.Pvp);
		var backToOnePvp = service.LeaveZone(1001, CreaturePvpZoneCounterType.Pvp);
		var noPvp = service.LeaveZone(1001, CreaturePvpZoneCounterType.Pvp);

		Assert.Equal(1, onePvp.PvpZoneCount);
		Assert.False(onePvp.IsInsidePvpZone);
		Assert.Equal(2, twoPvp.PvpZoneCount);
		Assert.True(twoPvp.IsInsidePvpZone);
		Assert.Equal(1, backToOnePvp.PvpZoneCount);
		Assert.False(backToOnePvp.IsInsidePvpZone);
		Assert.Equal(0, noPvp.PvpZoneCount);
		Assert.True(noPvp.IsInsidePvpZone);
		Assert.Equal(noPvp, service.GetCounters(1001));
	}

	[Fact]
	public void SiegeCounterOverridesPvpZoneCounter()
	{
		var service = new CreaturePvpZoneCounterService();

		service.EnterZone(1001, CreaturePvpZoneCounterType.Pvp);
		var siegeEntered = service.EnterZone(1001, CreaturePvpZoneCounterType.Siege);
		var siegeLeft = service.LeaveZone(1001, CreaturePvpZoneCounterType.Siege);

		Assert.Equal(1, siegeEntered.PvpZoneCount);
		Assert.Equal(1, siegeEntered.SiegeZoneCount);
		Assert.True(siegeEntered.IsInsidePvpZone);
		Assert.Equal(0, siegeLeft.SiegeZoneCount);
		Assert.False(siegeLeft.IsInsidePvpZone);
	}

	[Fact]
	public void LeaveWithoutMembershipDoesNotCreateNegativeCounters()
	{
		var service = new CreaturePvpZoneCounterService();

		var counters = service.LeaveZone(1001, CreaturePvpZoneCounterType.Pvp);

		Assert.Equal(0, counters.PvpZoneCount);
		Assert.Equal(0, counters.SiegeZoneCount);
		Assert.True(counters.IsInsidePvpZone);
	}

	[Fact]
	public void ClearCountersRemovesTrackedCreatureState()
	{
		var service = new CreaturePvpZoneCounterService();
		service.EnterZone(1001, CreaturePvpZoneCounterType.Pvp);
		service.EnterZone(1001, CreaturePvpZoneCounterType.Siege);

		var removed = service.ClearCounters(1001);

		Assert.True(removed);
		Assert.Equal(CreaturePvpZoneCounters.Empty, service.GetCounters(1001));
		Assert.False(service.ClearCounters(1001));
	}
}
