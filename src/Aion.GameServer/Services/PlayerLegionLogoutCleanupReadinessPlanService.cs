using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerLegionLogoutCleanupReadinessPlanService
{
	public static PlayerLegionLogoutCleanupReadinessPlan CreatePlan(
		Player? player,
		PlayerLegionLogoutCleanupPrerequisites prerequisites)
	{
		var playerObjectId = player?.ObjectId ?? 0;
		var legionId = player?.LegionId ?? 0;
		if (player == null || legionId <= 0)
		{
			return new PlayerLegionLogoutCleanupReadinessPlan(
				PlayerLegionLogoutCleanupReadinessStatus.SkippedNoLegion,
				playerObjectId,
				legionId,
				MissingCriteria: Array.Empty<PlayerLegionLogoutCleanupReadinessCriterion>(),
				WouldRunWarehouseUpdate: false,
				WouldRunMemberCleanup: false,
				ReadyForLiveLogoutWiring: false,
				IsLive: false,
				"LegionService.LegionWhUpdate returns when player.getLegion() is null; PlayerLeaveWorldService calls LegionService.onLogout only when player.isLegionMember() is true.",
				"Skipped because no modeled C# legion membership is available for this player.");
		}

		var missing = new List<PlayerLegionLogoutCleanupReadinessCriterion>();
		if (!prerequisites.LegionWarehouseRuntimeAvailable)
			missing.Add(PlayerLegionLogoutCleanupReadinessCriterion.LegionWarehouseRuntimeAvailable);
		if (!prerequisites.LegionWarehouseInUseStateAvailable)
			missing.Add(PlayerLegionLogoutCleanupReadinessCriterion.LegionWarehouseInUseStateAvailable);
		if (!prerequisites.LegionWarehouseItemPersistenceAvailable)
			missing.Add(PlayerLegionLogoutCleanupReadinessCriterion.LegionWarehouseItemPersistenceAvailable);
		if (!prerequisites.ItemStonePersistenceAvailable)
			missing.Add(PlayerLegionLogoutCleanupReadinessCriterion.ItemStonePersistenceAvailable);
		if (!prerequisites.LegionMemberRuntimeAvailable)
			missing.Add(PlayerLegionLogoutCleanupReadinessCriterion.LegionMemberRuntimeAvailable);
		if (!prerequisites.LegionRepositoryAvailable)
			missing.Add(PlayerLegionLogoutCleanupReadinessCriterion.LegionRepositoryAvailable);
		if (!prerequisites.LegionMemberRepositoryAvailable)
			missing.Add(PlayerLegionLogoutCleanupReadinessCriterion.LegionMemberRepositoryAvailable);
		if (!prerequisites.LegionMemberInfoFanoutAvailable)
			missing.Add(PlayerLegionLogoutCleanupReadinessCriterion.LegionMemberInfoFanoutAvailable);
		if (!prerequisites.LegionBonusFanoutAvailable)
			missing.Add(PlayerLegionLogoutCleanupReadinessCriterion.LegionBonusFanoutAvailable);
		if (!prerequisites.LogoutHookAvailable)
			missing.Add(PlayerLegionLogoutCleanupReadinessCriterion.LogoutHookAvailable);

		var ready = missing.Count == 0;
		return new PlayerLegionLogoutCleanupReadinessPlan(
			ready
				? PlayerLegionLogoutCleanupReadinessStatus.ReadyForLiveLogoutWiring
				: PlayerLegionLogoutCleanupReadinessStatus.NotReady,
			playerObjectId,
			legionId,
			missing,
			WouldRunWarehouseUpdate: true,
			WouldRunMemberCleanup: true,
			ReadyForLiveLogoutWiring: ready,
			IsLive: false,
			"PlayerLeaveWorldService.leaveWorld calls LegionService.LegionWhUpdate before effect cleanup, then LegionService.onLogout after player online/lastOnline mutation.",
			ready
				? "All modeled C# prerequisites are present; this plan still does not execute live persistence or packet fanout."
				: "C# is missing one or more legion warehouse, member, repository, fanout, or logout hook prerequisites required before live wiring.");
	}
}

public sealed record PlayerLegionLogoutCleanupPrerequisites(
	bool LegionWarehouseRuntimeAvailable = false,
	bool LegionWarehouseInUseStateAvailable = false,
	bool LegionWarehouseItemPersistenceAvailable = false,
	bool ItemStonePersistenceAvailable = false,
	bool LegionMemberRuntimeAvailable = false,
	bool LegionRepositoryAvailable = false,
	bool LegionMemberRepositoryAvailable = false,
	bool LegionMemberInfoFanoutAvailable = false,
	bool LegionBonusFanoutAvailable = false,
	bool LogoutHookAvailable = false);

public sealed record PlayerLegionLogoutCleanupReadinessPlan(
	PlayerLegionLogoutCleanupReadinessStatus Status,
	int PlayerObjectId,
	int LegionId,
	IReadOnlyList<PlayerLegionLogoutCleanupReadinessCriterion> MissingCriteria,
	bool WouldRunWarehouseUpdate,
	bool WouldRunMemberCleanup,
	bool ReadyForLiveLogoutWiring,
	bool IsLive,
	string JavaSource,
	string CSharpEvidence);

public enum PlayerLegionLogoutCleanupReadinessStatus
{
	SkippedNoLegion,
	NotReady,
	ReadyForLiveLogoutWiring,
}

public enum PlayerLegionLogoutCleanupReadinessCriterion
{
	LegionWarehouseRuntimeAvailable,
	LegionWarehouseInUseStateAvailable,
	LegionWarehouseItemPersistenceAvailable,
	ItemStonePersistenceAvailable,
	LegionMemberRuntimeAvailable,
	LegionRepositoryAvailable,
	LegionMemberRepositoryAvailable,
	LegionMemberInfoFanoutAvailable,
	LegionBonusFanoutAvailable,
	LogoutHookAvailable,
}
