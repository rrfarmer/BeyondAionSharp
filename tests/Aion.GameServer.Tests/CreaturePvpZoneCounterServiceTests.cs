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
		service.ApplyZoneEnter(1001, "pvp-zone", CreaturePvpZoneCounterType.Pvp);
		service.ApplyZoneEnter(1001, "siege-zone", CreaturePvpZoneCounterType.Siege);

		var removed = service.ClearCounters(1001);
		var ignoredLeave = service.ApplyZoneLeave(1001, "pvp-zone", CreaturePvpZoneCounterType.Pvp);

		Assert.True(removed);
		Assert.Equal(CreaturePvpZoneCounters.Empty, service.GetCounters(1001));
		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.NotInside, ignoredLeave.Status);
		Assert.False(service.ClearCounters(1001));
	}

	[Fact]
	public void ApplyZoneEnterIgnoresDuplicateZoneMembership()
	{
		var service = new CreaturePvpZoneCounterService();

		var entered = service.ApplyZoneEnter(1001, "pvp-zone", CreaturePvpZoneCounterType.Pvp);
		var duplicateEnter = service.ApplyZoneEnter(1001, "pvp-zone", CreaturePvpZoneCounterType.Pvp);
		var left = service.ApplyZoneLeave(1001, "pvp-zone", CreaturePvpZoneCounterType.Pvp);
		var duplicateLeave = service.ApplyZoneLeave(1001, "pvp-zone", CreaturePvpZoneCounterType.Pvp);

		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.Entered, entered.Status);
		Assert.True(entered.Applied);
		Assert.Equal(1, entered.Counters.PvpZoneCount);
		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.AlreadyInside, duplicateEnter.Status);
		Assert.False(duplicateEnter.Applied);
		Assert.Equal(1, duplicateEnter.Counters.PvpZoneCount);
		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.Left, left.Status);
		Assert.True(left.Applied);
		Assert.Equal(0, left.Counters.PvpZoneCount);
		Assert.Equal(CreaturePvpZoneMembershipTransitionStatus.NotInside, duplicateLeave.Status);
		Assert.False(duplicateLeave.Applied);
	}

	[Fact]
	public void ApplyZoneMembershipTracksNestedDifferentZones()
	{
		var service = new CreaturePvpZoneCounterService();

		var firstZone = service.ApplyZoneEnter(1001, "pvp-zone-a", CreaturePvpZoneCounterType.Pvp);
		var secondZone = service.ApplyZoneEnter(1001, "pvp-zone-b", CreaturePvpZoneCounterType.Pvp);
		var afterLeavingFirst = service.ApplyZoneLeave(1001, "pvp-zone-a", CreaturePvpZoneCounterType.Pvp);

		Assert.Equal(1, firstZone.Counters.PvpZoneCount);
		Assert.False(firstZone.Counters.IsInsidePvpZone);
		Assert.Equal(2, secondZone.Counters.PvpZoneCount);
		Assert.True(secondZone.Counters.IsInsidePvpZone);
		Assert.Equal(1, afterLeavingFirst.Counters.PvpZoneCount);
		Assert.False(afterLeavingFirst.Counters.IsInsidePvpZone);
	}
}
