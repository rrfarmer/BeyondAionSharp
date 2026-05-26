using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahPersistenceStatus
{
	Saved,
	MissingRow,
	Failed,
}

public sealed record BindPointTeleportKinahPersistenceResult(
	BindPointTeleportKinahPersistenceStatus Status,
	int PlayerObjectId,
	int KinahObjectId,
	long KinahCount,
	bool ShouldRollbackInMemoryMutation,
	string JavaSource,
	bool IsLive);

public enum BindPointTeleportKinahPersistenceDecisionStatus
{
	StoppedNotEnoughKinah,
	ContinueWithoutPersistence,
	ContinueAfterPersistence,
	StoppedMissingRow,
	StoppedFailed,
	StoppedMissingPersistenceResult,
}

public sealed record BindPointTeleportKinahPersistenceDecision(
	BindPointTeleportKinahPersistenceDecisionStatus Status,
	BindPointTeleportScheduledCallbackPlan CallbackPlan,
	BindPointTeleportKinahPersistenceResult? PersistenceResult,
	InventoryItem? KinahItemUpdate,
	int? KinahInventoryUpdateType,
	bool ShouldSendNotEnoughFee,
	bool ShouldEmitKinahInventoryUpdatePacket,
	bool ShouldRollbackInMemoryMutation,
	bool ShouldContinueToCooldownFanout,
	bool ShouldScheduleFinalTeleport,
	bool ShouldTeleport,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKinahPersistenceDecisionBridgeService
{
	public static BindPointTeleportKinahPersistenceDecision CreateDecision(
		BindPointTeleportScheduledCallbackPlan callbackPlan,
		BindPointTeleportKinahPersistenceResult? persistenceResult)
	{
		// Java parity: the scheduled callback only proceeds to addCooldown and action 3 fanout after
		// tryDecreaseKinah succeeds. This C# bridge stages the extra persist-before-send gate required
		// by the current C# policy before inventory packet metadata may continue.
		if (callbackPlan.ShouldSendNotEnoughFee || !callbackPlan.KinahPlan.ShouldContinueScheduledTeleport)
		{
			return Stop(
				BindPointTeleportKinahPersistenceDecisionStatus.StoppedNotEnoughKinah,
				callbackPlan,
				persistenceResult: null,
				shouldSendNotEnoughFee: callbackPlan.ShouldSendNotEnoughFee,
				shouldRollbackInMemoryMutation: false,
				"BindPointTeleportService.teleport scheduled task -> tryDecreaseKinah failed -> send fee message and return before persistence, packet send, cooldown, fanout, and movement");
		}

		if (!callbackPlan.ShouldEmitKinahInventoryUpdatePacket)
		{
			return new BindPointTeleportKinahPersistenceDecision(
				BindPointTeleportKinahPersistenceDecisionStatus.ContinueWithoutPersistence,
				callbackPlan,
				PersistenceResult: null,
				KinahItemUpdate: null,
				KinahInventoryUpdateType: null,
				ShouldSendNotEnoughFee: false,
				ShouldEmitKinahInventoryUpdatePacket: false,
				ShouldRollbackInMemoryMutation: false,
				ShouldContinueToCooldownFanout: callbackPlan.ShouldStoreCooldown && callbackPlan.ShouldBroadcastCooldown,
				callbackPlan.ShouldScheduleFinalTeleport,
				callbackPlan.ShouldTeleport,
				"Storage.decreaseKinah amount > 0 guard means no Kinah item update or persistence is required for this staged callback",
				IsLive: false);
		}

		if (persistenceResult == null)
		{
			return Stop(
				BindPointTeleportKinahPersistenceDecisionStatus.StoppedMissingPersistenceResult,
				callbackPlan,
				persistenceResult: null,
				shouldSendNotEnoughFee: false,
				shouldRollbackInMemoryMutation: true,
				"C# staging guard: Kinah update metadata requires an explicit persistence result before packet send, cooldown, fanout, or movement can continue");
		}

		if (persistenceResult.Status != BindPointTeleportKinahPersistenceStatus.Saved)
		{
			return Stop(
				persistenceResult.Status == BindPointTeleportKinahPersistenceStatus.MissingRow
					? BindPointTeleportKinahPersistenceDecisionStatus.StoppedMissingRow
					: BindPointTeleportKinahPersistenceDecisionStatus.StoppedFailed,
				callbackPlan,
				persistenceResult,
				shouldSendNotEnoughFee: false,
				shouldRollbackInMemoryMutation: true,
				"Scheduled bind-point Kinah persistence did not save the owner-checked row; C# policy stops before inventory packet send, cooldown, action 3 fanout, and movement");
		}

		return new BindPointTeleportKinahPersistenceDecision(
			BindPointTeleportKinahPersistenceDecisionStatus.ContinueAfterPersistence,
			callbackPlan,
			persistenceResult,
			callbackPlan.KinahItemUpdate,
			callbackPlan.KinahInventoryUpdateType,
			ShouldSendNotEnoughFee: false,
			callbackPlan.ShouldEmitKinahInventoryUpdatePacket,
			ShouldRollbackInMemoryMutation: false,
			ShouldContinueToCooldownFanout: callbackPlan.ShouldStoreCooldown && callbackPlan.ShouldBroadcastCooldown,
			callbackPlan.ShouldScheduleFinalTeleport,
			callbackPlan.ShouldTeleport,
			"Scheduled bind-point Kinah persistence saved the owner-checked row; C# may now emit the staged inventory update packet metadata and continue to cooldown/action 3 fanout",
			IsLive: false);
	}

	private static BindPointTeleportKinahPersistenceDecision Stop(
		BindPointTeleportKinahPersistenceDecisionStatus status,
		BindPointTeleportScheduledCallbackPlan callbackPlan,
		BindPointTeleportKinahPersistenceResult? persistenceResult,
		bool shouldSendNotEnoughFee,
		bool shouldRollbackInMemoryMutation,
		string javaSource)
	{
		return new BindPointTeleportKinahPersistenceDecision(
			status,
			callbackPlan,
			persistenceResult,
			KinahItemUpdate: null,
			KinahInventoryUpdateType: null,
			shouldSendNotEnoughFee,
			ShouldEmitKinahInventoryUpdatePacket: false,
			shouldRollbackInMemoryMutation,
			ShouldContinueToCooldownFanout: false,
			ShouldScheduleFinalTeleport: false,
			ShouldTeleport: false,
			javaSource,
			IsLive: false);
	}
}
