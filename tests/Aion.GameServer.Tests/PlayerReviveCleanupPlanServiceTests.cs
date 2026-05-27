using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerReviveCleanupPlanServiceTests
{
	[Fact]
	public void CreateKiskReviveCleanupPlan_ComposesAggroClearInJavaReviveOrder()
	{
		var service = new PlayerReviveCleanupPlanService();
		var aggroEntries = new[]
		{
			new PlayerAggroEntrySnapshot(2001, Damage: 80, Hate: 800),
			new PlayerAggroEntrySnapshot(2002, Damage: 20, Hate: 200),
		};

		var plan = service.CreateKiskReviveCleanupPlan(PlayerObjectId, aggroEntries);

		Assert.Equal(PlayerObjectId, plan.PlayerObjectId);
		Assert.Equal(
			[
				PlayerReviveCleanupPlanStep.ClearKnownPlayerTargets,
				PlayerReviveCleanupPlanStep.ApplyHpMpDpAndResurrectionState,
				PlayerReviveCleanupPlanStep.ClearPlayerAggro,
				PlayerReviveCleanupPlanStep.OnBeforeSpawn,
				PlayerReviveCleanupPlanStep.GroupAllianceMovementUpdate,
				PlayerReviveCleanupPlanStep.BroadcastResurrectEmotion,
			],
			plan.Steps);
		Assert.True(plan.PlacesAggroClearAfterRestore);
		Assert.True(plan.PlacesAggroClearBeforeSpawn);
		Assert.Equal(PlayerAggroCleanupReason.Revive, plan.AggroClearPlan.Reason);
		Assert.Equal(aggroEntries, plan.AggroClearPlan.ClearedEntries);
		Assert.Contains("PlayerReviveService.revive", plan.JavaSource);
		Assert.False(plan.IsLive);
	}

	private const int PlayerObjectId = 1001;
}
