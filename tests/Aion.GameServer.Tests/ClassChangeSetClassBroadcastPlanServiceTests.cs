using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ClassChangeSetClassBroadcastPlanServiceTests
{
	[Fact]
	public void CreatePlan_ProducesClassChangeAnimationBroadcast()
	{
		var plan = ClassChangeSetClassBroadcastPlanService.CreatePlan(playerObjectId: 9001, playerLevel: 10);

		Assert.Equal(ClassChangeSetClassBroadcastPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldBroadcastAnimation);
		Assert.True(plan.ShouldBroadcastPlayerInfo);
		Assert.NotNull(plan.ClassChangeAnimationPacket);
		Assert.Single(plan.BroadcastPacketsInOrder);
		Assert.IsType<SmActionAnimation>(plan.BroadcastPacketsInOrder[0]);
		Assert.Contains("CLASS_CHANGE", plan.JavaSource);
		Assert.Contains("SM_PLAYER_INFO", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_RecordsPlayerLevel()
	{
		var plan = ClassChangeSetClassBroadcastPlanService.CreatePlan(playerObjectId: 9001, playerLevel: 25);

		Assert.Equal(25, plan.PlayerLevel);
	}

	[Fact]
	public void SmActionAnimation_ClassChangeAndCraftLevelUpShareId4()
	{
		// Java: ActionAnimation.CLASS_CHANGE(4) and CRAFT_LEVEL_UP(4) share the same id
		Assert.Equal(SmActionAnimation.CraftLevelUp, SmActionAnimation.ClassChange);
		Assert.Equal(4, SmActionAnimation.ClassChange);
	}

	[Fact]
	public void CreatePlan_RecordsLiveSideEffectsNote()
	{
		var plan = ClassChangeSetClassBroadcastPlanService.CreatePlan(playerObjectId: 9001, playerLevel: 10);

		Assert.Contains("live player state", plan.JavaSource);
		Assert.Contains("SM_PLAYER_INFO", plan.JavaSource);
	}
}
