using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerAggroCleanupPlanServiceTests
{
	[Fact]
	public void PlanAwareness_UsesPlayerAggroListKnownOnlyRule()
	{
		var service = new PlayerAggroCleanupPlanService();

		var known = service.PlanAwareness(OwnerPlayerObjectId, AttackerObjectId, ownerKnownListKnowsAttacker: true);
		var unknown = service.PlanAwareness(OwnerPlayerObjectId, AttackerObjectId, ownerKnownListKnowsAttacker: false);

		Assert.Equal(PlayerAggroAwarenessStatus.AcceptedKnownObject, known.Status);
		Assert.Equal(PlayerAggroAwarenessStatus.RejectedUnknownObject, unknown.Status);
		Assert.True(known.UsesKnownListOnlyAwareness);
		Assert.Contains("PlayerAggroList.isAware", known.JavaSource);
		Assert.False(known.IsLive);
	}

	[Fact]
	public void PlanClear_ReviveClearsAllPlayerAggroEntries()
	{
		var service = new PlayerAggroCleanupPlanService();
		var entries = new[]
		{
			new PlayerAggroEntrySnapshot(AttackerObjectId, Damage: 120, Hate: 1200),
			new PlayerAggroEntrySnapshot(SecondAttackerObjectId, Damage: 40, Hate: 400),
		};

		var plan = service.PlanClear(OwnerPlayerObjectId, entries, PlayerAggroCleanupReason.Revive);

		Assert.Equal(PlayerAggroCleanupReason.Revive, plan.Reason);
		Assert.Equal(entries, plan.ClearedEntries);
		Assert.True(plan.ClearsAllEntries);
		Assert.True(plan.CancelsHateReductionTask);
		Assert.Contains("PlayerReviveService.revive", plan.JavaSource);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void PlanClear_FullHpRestoreUsesPlayerLifeStatsSource()
	{
		var service = new PlayerAggroCleanupPlanService();

		var plan = service.PlanClear(
			OwnerPlayerObjectId,
			[new PlayerAggroEntrySnapshot(AttackerObjectId, Damage: 20, Hate: 200)],
			PlayerAggroCleanupReason.FullHpRestore);

		Assert.Equal(PlayerAggroCleanupReason.FullHpRestore, plan.Reason);
		Assert.True(plan.ClearsAllEntries);
		Assert.True(plan.CancelsHateReductionTask);
		Assert.Contains("PlayerLifeStats.onHpChanged", plan.JavaSource);
		Assert.False(plan.IsLive);
	}

	private const int OwnerPlayerObjectId = 1001;
	private const int AttackerObjectId = 2001;
	private const int SecondAttackerObjectId = 2002;
}
