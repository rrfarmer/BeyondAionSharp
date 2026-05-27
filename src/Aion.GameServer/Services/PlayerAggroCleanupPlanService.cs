namespace Aion.GameServer.Services;

public enum PlayerAggroCleanupReason
{
	Revive,
	FullHpRestore,
}

public enum PlayerAggroAwarenessStatus
{
	AcceptedKnownObject,
	RejectedUnknownObject,
}

public sealed record PlayerAggroEntrySnapshot(
	int AttackerObjectId,
	int Damage,
	int Hate);

public sealed record PlayerAggroAwarenessPlan(
	int OwnerPlayerObjectId,
	int AttackerObjectId,
	bool OwnerKnownListKnowsAttacker,
	PlayerAggroAwarenessStatus Status,
	bool UsesKnownListOnlyAwareness,
	string JavaSource,
	bool IsLive);

public sealed record PlayerAggroClearPlan(
	int OwnerPlayerObjectId,
	PlayerAggroCleanupReason Reason,
	IReadOnlyList<PlayerAggroEntrySnapshot> ClearedEntries,
	bool ClearsAllEntries,
	bool CancelsHateReductionTask,
	string JavaSource,
	bool IsLive);

public sealed class PlayerAggroCleanupPlanService
{
	public PlayerAggroAwarenessPlan PlanAwareness(
		int ownerPlayerObjectId,
		int attackerObjectId,
		bool ownerKnownListKnowsAttacker)
	{
		// Java parity breadcrumb: PlayerAggroList.isAware only requires
		// owner.getKnownList().knows(creature), unlike ordinary AggroList.
		return new PlayerAggroAwarenessPlan(
			ownerPlayerObjectId,
			attackerObjectId,
			ownerKnownListKnowsAttacker,
			ownerKnownListKnowsAttacker
				? PlayerAggroAwarenessStatus.AcceptedKnownObject
				: PlayerAggroAwarenessStatus.RejectedUnknownObject,
			UsesKnownListOnlyAwareness: true,
			"com.aionemu.gameserver.controllers.attack.PlayerAggroList.isAware -> creature != null && owner.getKnownList().knows(creature)",
			IsLive: false);
	}

	public PlayerAggroClearPlan PlanClear(
		int ownerPlayerObjectId,
		IEnumerable<PlayerAggroEntrySnapshot> entries,
		PlayerAggroCleanupReason reason)
	{
		// Java parity breadcrumb: PlayerReviveService.revive calls
		// player.getAggroList().clear(); PlayerLifeStats also clears on full HP.
		return new PlayerAggroClearPlan(
			ownerPlayerObjectId,
			reason,
			entries.ToArray(),
			ClearsAllEntries: true,
			CancelsHateReductionTask: true,
			reason == PlayerAggroCleanupReason.Revive
				? "com.aionemu.gameserver.services.player.PlayerReviveService.revive -> player.getAggroList().clear()"
				: "com.aionemu.gameserver.model.stats.container.PlayerLifeStats.onHpChanged -> owner.getAggroList().clear() when fully restored",
			IsLive: false);
	}
}
