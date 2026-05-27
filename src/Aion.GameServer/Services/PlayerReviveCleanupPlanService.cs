namespace Aion.GameServer.Services;

public enum PlayerReviveCleanupPlanStep
{
	ClearKnownPlayerTargets,
	ApplyHpMpDpAndResurrectionState,
	ClearPlayerAggro,
	OnBeforeSpawn,
	GroupAllianceMovementUpdate,
	BroadcastResurrectEmotion,
}

public sealed record PlayerReviveCleanupPlan(
	int PlayerObjectId,
	IReadOnlyList<PlayerReviveCleanupPlanStep> Steps,
	PlayerAggroClearPlan AggroClearPlan,
	bool PlacesAggroClearAfterRestore,
	bool PlacesAggroClearBeforeSpawn,
	string JavaSource,
	bool IsLive);

public sealed class PlayerReviveCleanupPlanService
{
	private readonly PlayerAggroCleanupPlanService _aggroCleanupPlanService;

	public PlayerReviveCleanupPlanService(PlayerAggroCleanupPlanService? aggroCleanupPlanService = null)
	{
		_aggroCleanupPlanService = aggroCleanupPlanService ?? new PlayerAggroCleanupPlanService();
	}

	public PlayerReviveCleanupPlan CreateKiskReviveCleanupPlan(
		int playerObjectId,
		IEnumerable<PlayerAggroEntrySnapshot> preReviveAggroEntries,
		bool isLive = false)
	{
		// Java parity breadcrumb: PlayerReviveService.kiskRevive delegates to
		// revive(player, 30, 30, false, skillId), whose cleanup order clears
		// known player targets, restores resources/res state, clears aggro,
		// runs onBeforeSpawn, sends team movement updates, then broadcasts RESURRECT.
		var aggroClearPlan = _aggroCleanupPlanService.PlanClear(
			playerObjectId,
			preReviveAggroEntries,
			PlayerAggroCleanupReason.Revive,
			isLive);
		var steps = new[]
		{
			PlayerReviveCleanupPlanStep.ClearKnownPlayerTargets,
			PlayerReviveCleanupPlanStep.ApplyHpMpDpAndResurrectionState,
			PlayerReviveCleanupPlanStep.ClearPlayerAggro,
			PlayerReviveCleanupPlanStep.OnBeforeSpawn,
			PlayerReviveCleanupPlanStep.GroupAllianceMovementUpdate,
			PlayerReviveCleanupPlanStep.BroadcastResurrectEmotion,
		};

		return new PlayerReviveCleanupPlan(
			playerObjectId,
			steps,
			aggroClearPlan,
			PlacesAggroClearAfterRestore: true,
			PlacesAggroClearBeforeSpawn: true,
			"com.aionemu.gameserver.services.player.PlayerReviveService.revive cleanup order inside kiskRevive",
			isLive);
	}
}
