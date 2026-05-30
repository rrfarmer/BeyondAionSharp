using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AbyssAnnouncementPlanServiceTests
{
	[Fact]
	public void CreateHighRankedDeathPlan_ValidRankAndMapShouldAnnounce()
	{
		var plan = AbyssAnnouncementPlanService.CreateHighRankedDeathAnnouncementPlan(
			rankId: 9, worldId: 210050000, "Elyos", "Private First Class", "Daeva", "Reshanta");

		Assert.Equal(AbyssAnnouncementPlanStatus.ShouldAnnounce, plan.Status);
		Assert.True(plan.ShouldBroadcastToWorld);
		Assert.NotNull(plan.AnnouncementMessage);
		Assert.Equal(1400023, plan.AnnouncementMessage!.MessageId);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateHighRankedDeathPlan_RankBelowGrade1SoldierSkips()
	{
		var plan = AbyssAnnouncementPlanService.CreateHighRankedDeathAnnouncementPlan(
			rankId: 8, worldId: 210050000, "Elyos", "Private", "Daeva", "Reshanta");

		Assert.Equal(AbyssAnnouncementPlanStatus.SkippedRankTooLow, plan.Status);
		Assert.False(plan.ShouldBroadcastToWorld);
		Assert.Null(plan.AnnouncementMessage);
	}

	[Fact]
	public void CreateHighRankedDeathPlan_NotInAnnounceMapSkips()
	{
		var plan = AbyssAnnouncementPlanService.CreateHighRankedDeathAnnouncementPlan(
			rankId: 15, worldId: 110010000, "Elyos", "General", "Daeva", "Poeta");

		Assert.Equal(AbyssAnnouncementPlanStatus.SkippedNotInAnnounceMap, plan.Status);
		Assert.False(plan.ShouldBroadcastToWorld);
	}

	[Theory]
	[InlineData(210050000)]
	[InlineData(400010000)]
	[InlineData(600100000)]
	public void KillAnnounceMaps_ContainsExpectedMaps(int mapId)
	{
		Assert.Contains(mapId, AbyssAnnouncementPlanService.KillAnnounceMaps);
	}

	[Fact]
	public void KillAnnounceMaps_Contains14Maps()
	{
		Assert.Equal(14, AbyssAnnouncementPlanService.KillAnnounceMaps.Count);
	}

	[Fact]
	public void MinAnnounceRankIdIs9()
	{
		Assert.Equal(9, AbyssAnnouncementPlanService.MinAnnounceRankId);
	}
}
