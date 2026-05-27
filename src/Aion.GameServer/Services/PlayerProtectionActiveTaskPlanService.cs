using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskPlanStatus
{
	StartProtection,
	AlreadyProtected,
	StopProtection,
	StopProtectionUnspawned,
}

public enum PlayerProtectionActiveTaskPlanStep
{
	CheckProtectionActive,
	SetBlinkingVisualState,
	CancelCastOnPlayer,
	RemovePlayerFromTargets,
	BroadcastPlayerState,
	ScheduleProtectionActiveTask,
	StoreProtectionActiveTask,
	CancelProtectionActiveTask,
	UnsetBlinkingVisualState,
	NotifyAiOnMove,
}

public sealed record PlayerProtectionActiveTaskPlan(
	PlayerProtectionActiveTaskPlanStatus Status,
	int PlayerObjectId,
	bool WasProtectionActive,
	bool IsSpawned,
	bool ShouldSetBlinkingVisualState,
	bool ShouldUnsetBlinkingVisualState,
	bool ShouldCancelCastOnPlayer,
	bool ShouldRemovePlayerFromTargets,
	bool ShouldBroadcastPlayerState,
	bool ShouldScheduleTask,
	bool ShouldStoreTask,
	bool ShouldCancelTask,
	bool ShouldNotifyAiOnMove,
	int DelayMilliseconds,
	string TaskIdName,
	int TaskIdOrdinal,
	Type? BroadcastPacketType,
	IReadOnlyList<PlayerProtectionActiveTaskPlanStep> Steps,
	string JavaSource,
	bool IsLive);

public static class PlayerProtectionActiveTaskPlanService
{
	public const string ProtectionActiveTaskIdName = "TaskId.PROTECTION_ACTIVE";
	public const int ProtectionActiveTaskIdOrdinal = 3;
	public const int ProtectionActiveDelayMilliseconds = 60_000;

	public static PlayerProtectionActiveTaskPlan CreateStartPlan(Player player)
	{
		// Java parity: PlayerController.startProtectionActiveTask no-ops while BLINKING is already active.
		if (player.IsProtectionActive())
		{
			return new PlayerProtectionActiveTaskPlan(
				PlayerProtectionActiveTaskPlanStatus.AlreadyProtected,
				player.ObjectId,
				WasProtectionActive: true,
				IsSpawned: true,
				ShouldSetBlinkingVisualState: false,
				ShouldUnsetBlinkingVisualState: false,
				ShouldCancelCastOnPlayer: false,
				ShouldRemovePlayerFromTargets: false,
				ShouldBroadcastPlayerState: false,
				ShouldScheduleTask: false,
				ShouldStoreTask: false,
				ShouldCancelTask: false,
				ShouldNotifyAiOnMove: false,
				DelayMilliseconds: 0,
				ProtectionActiveTaskIdName,
				ProtectionActiveTaskIdOrdinal,
				BroadcastPacketType: null,
				[PlayerProtectionActiveTaskPlanStep.CheckProtectionActive],
				"com.aionemu.gameserver.controllers.PlayerController.startProtectionActiveTask -> if protection active, no-op",
				IsLive: false);
		}

		return new PlayerProtectionActiveTaskPlan(
			PlayerProtectionActiveTaskPlanStatus.StartProtection,
			player.ObjectId,
			WasProtectionActive: false,
			IsSpawned: true,
			ShouldSetBlinkingVisualState: true,
			ShouldUnsetBlinkingVisualState: false,
			ShouldCancelCastOnPlayer: true,
			ShouldRemovePlayerFromTargets: true,
			ShouldBroadcastPlayerState: true,
			ShouldScheduleTask: true,
			ShouldStoreTask: true,
			ShouldCancelTask: false,
			ShouldNotifyAiOnMove: false,
			ProtectionActiveDelayMilliseconds,
			ProtectionActiveTaskIdName,
			ProtectionActiveTaskIdOrdinal,
			typeof(SmPlayerState),
			[
				PlayerProtectionActiveTaskPlanStep.CheckProtectionActive,
				PlayerProtectionActiveTaskPlanStep.SetBlinkingVisualState,
				PlayerProtectionActiveTaskPlanStep.CancelCastOnPlayer,
				PlayerProtectionActiveTaskPlanStep.RemovePlayerFromTargets,
				PlayerProtectionActiveTaskPlanStep.BroadcastPlayerState,
				PlayerProtectionActiveTaskPlanStep.ScheduleProtectionActiveTask,
				PlayerProtectionActiveTaskPlanStep.StoreProtectionActiveTask,
			],
			"com.aionemu.gameserver.controllers.PlayerController.startProtectionActiveTask -> set BLINKING, AttackUtil.cancelCastOn/removeTargetFrom, broadcast SM_PLAYER_STATE, add TaskId.PROTECTION_ACTIVE scheduled after 60000ms",
			IsLive: false);
	}

	public static PlayerProtectionActiveTaskPlan CreateStopPlan(Player player, bool hasProtectionActiveTask, bool isSpawned)
	{
		// Java parity: PlayerController.stopProtectionActiveTask always cancels TaskId.PROTECTION_ACTIVE,
		// then only clears BLINKING/broadcasts/notifies AI when player.isSpawned().
		var steps = new List<PlayerProtectionActiveTaskPlanStep>
		{
			PlayerProtectionActiveTaskPlanStep.CancelProtectionActiveTask,
		};
		if (isSpawned)
		{
			steps.Add(PlayerProtectionActiveTaskPlanStep.UnsetBlinkingVisualState);
			steps.Add(PlayerProtectionActiveTaskPlanStep.BroadcastPlayerState);
			steps.Add(PlayerProtectionActiveTaskPlanStep.NotifyAiOnMove);
		}

		return new PlayerProtectionActiveTaskPlan(
			isSpawned
				? PlayerProtectionActiveTaskPlanStatus.StopProtection
				: PlayerProtectionActiveTaskPlanStatus.StopProtectionUnspawned,
			player.ObjectId,
			player.IsProtectionActive(),
			isSpawned,
			ShouldSetBlinkingVisualState: false,
			ShouldUnsetBlinkingVisualState: isSpawned,
			ShouldCancelCastOnPlayer: false,
			ShouldRemovePlayerFromTargets: false,
			ShouldBroadcastPlayerState: isSpawned,
			ShouldScheduleTask: false,
			ShouldStoreTask: false,
			ShouldCancelTask: hasProtectionActiveTask,
			ShouldNotifyAiOnMove: isSpawned,
			DelayMilliseconds: 0,
			ProtectionActiveTaskIdName,
			ProtectionActiveTaskIdOrdinal,
			isSpawned ? typeof(SmPlayerState) : null,
			steps,
			"com.aionemu.gameserver.controllers.PlayerController.stopProtectionActiveTask -> cancel TaskId.PROTECTION_ACTIVE; if spawned, unset BLINKING, broadcast SM_PLAYER_STATE, notifyAIOnMove",
			IsLive: false);
	}
}
