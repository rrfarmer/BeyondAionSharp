using Aion.GameServer.Network.Aion;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum BindPointTeleportScheduledCallbackPlanStatus
{
	StoppedNotEnoughKinah,
	ReadyWithMovement,
	ReadyWithoutMovement,
}

public enum BindPointTeleportScheduledCallbackPlanStep
{
	TryDecreaseKinahFly,
	SendNotEnoughFeeAndReturn,
	AddCooldown,
	BroadcastCooldown,
	ScheduleFinalTeleport,
	CheckFinalMovementGate,
	CreateFinalMovementIntent,
	CreateTeleportSideEffectIntent,
	CreateKinahMutationIntent,
}

public sealed record BindPointTeleportScheduledCallbackPlan(
	BindPointTeleportScheduledCallbackPlanStatus Status,
	BindPointTeleportScheduledKinahPlan KinahPlan,
	BindPointTeleportScheduledKinahMutationPlan? KinahMutationPlan,
	BindPointTeleportCooldownPlan? CooldownPlan,
	BindPointTeleportFanoutPlan? CooldownFanoutPlan,
	BindPointTeleportFinalMovementPlan? FinalMovementPlan,
	BindPointTeleportTeleportToSideEffectPlan? TeleportSideEffectPlan,
	InventoryItem? KinahItemUpdate,
	int? KinahInventoryUpdateType,
	IReadOnlyList<BindPointTeleportScheduledCallbackPlanStep> Steps,
	bool ShouldSendNotEnoughFee,
	bool ShouldEmitKinahInventoryUpdatePacket,
	bool ShouldStoreCooldown,
	bool ShouldBroadcastCooldown,
	bool ShouldScheduleFinalTeleport,
	bool ShouldTeleport,
	bool ShouldPlanTeleportSideEffects,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportScheduledCallbackPlanService
{
	public static BindPointTeleportScheduledCallbackPlan CreatePlan(
		BindPointTeleportScheduledKinahPlan kinahPlan,
		BindPointTeleportCooldownPlan cooldownPlan,
		BindPointTeleportFanoutPlan cooldownFanoutPlan,
		BindPointTeleportFinalMovementPlan finalMovementPlan,
		BindPointTeleportTeleportToSideEffectPlan? teleportSideEffectPlan = null,
		BindPointTeleportScheduledKinahMutationPlan? kinahMutationPlan = null)
	{
		// Java parity: BindPointTeleportService.teleport scheduled SKILL_USE callback.
		// This only composes intent/order; no inventory, cooldown map, packet, scheduler, or movement side effects run here.
		var kinahContinues = kinahMutationPlan?.Status == BindPointTeleportScheduledKinahMutationPlanStatus.DecrementReady
			|| (kinahMutationPlan == null && kinahPlan.ShouldContinueScheduledTeleport);
		if (!kinahContinues)
		{
			return new BindPointTeleportScheduledCallbackPlan(
				BindPointTeleportScheduledCallbackPlanStatus.StoppedNotEnoughKinah,
				kinahPlan,
				kinahMutationPlan,
				CooldownPlan: null,
				CooldownFanoutPlan: null,
				FinalMovementPlan: null,
				TeleportSideEffectPlan: null,
				KinahItemUpdate: null,
				KinahInventoryUpdateType: null,
				[
					BindPointTeleportScheduledCallbackPlanStep.TryDecreaseKinahFly,
					BindPointTeleportScheduledCallbackPlanStep.SendNotEnoughFeeAndReturn,
				],
				ShouldSendNotEnoughFee: kinahMutationPlan?.ShouldSendNotEnoughFee ?? kinahPlan.ShouldSendNotEnoughFee,
				ShouldEmitKinahInventoryUpdatePacket: false,
				ShouldStoreCooldown: false,
				ShouldBroadcastCooldown: false,
				ShouldScheduleFinalTeleport: false,
				ShouldTeleport: false,
				ShouldPlanTeleportSideEffects: false,
				"BindPointTeleportService.teleport scheduled task -> tryDecreaseKinah failed -> send STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE and return before cooldown/fanout/final teleport",
				IsLive: false);
		}

		var shouldTeleport = finalMovementPlan.ShouldTeleport;
		var shouldPlanSideEffects = shouldTeleport
			&& teleportSideEffectPlan != null
			&& teleportSideEffectPlan.Status != BindPointTeleportTeleportToSideEffectPlanStatus.BlockedFinalMovement;
		return new BindPointTeleportScheduledCallbackPlan(
			shouldTeleport
				? BindPointTeleportScheduledCallbackPlanStatus.ReadyWithMovement
				: BindPointTeleportScheduledCallbackPlanStatus.ReadyWithoutMovement,
			kinahPlan,
			kinahMutationPlan,
			cooldownPlan,
			cooldownFanoutPlan,
			finalMovementPlan,
			shouldPlanSideEffects ? teleportSideEffectPlan : null,
			kinahMutationPlan?.KinahItemUpdate,
			kinahMutationPlan?.InventoryUpdateType,
			[
				BindPointTeleportScheduledCallbackPlanStep.TryDecreaseKinahFly,
				.. kinahMutationPlan?.ShouldEmitInventoryUpdatePacket == true
					? [BindPointTeleportScheduledCallbackPlanStep.CreateKinahMutationIntent]
					: Array.Empty<BindPointTeleportScheduledCallbackPlanStep>(),
				BindPointTeleportScheduledCallbackPlanStep.AddCooldown,
				BindPointTeleportScheduledCallbackPlanStep.BroadcastCooldown,
				BindPointTeleportScheduledCallbackPlanStep.ScheduleFinalTeleport,
				BindPointTeleportScheduledCallbackPlanStep.CheckFinalMovementGate,
				.. shouldTeleport
					? [BindPointTeleportScheduledCallbackPlanStep.CreateFinalMovementIntent]
					: Array.Empty<BindPointTeleportScheduledCallbackPlanStep>(),
				.. shouldPlanSideEffects
					? [BindPointTeleportScheduledCallbackPlanStep.CreateTeleportSideEffectIntent]
					: Array.Empty<BindPointTeleportScheduledCallbackPlanStep>(),
			],
			ShouldSendNotEnoughFee: false,
			ShouldEmitKinahInventoryUpdatePacket: kinahMutationPlan?.ShouldEmitInventoryUpdatePacket == true,
			ShouldStoreCooldown: cooldownPlan.ShouldStoreCooldown,
			ShouldBroadcastCooldown: cooldownFanoutPlan.Packet is GameServerPacket,
			ShouldScheduleFinalTeleport: true,
			ShouldTeleport: shouldTeleport,
			ShouldPlanTeleportSideEffects: shouldPlanSideEffects,
			"BindPointTeleportService.teleport scheduled task -> tryDecreaseKinah succeeded -> addCooldown -> broadcast action 3 -> schedule 1000ms final teleport gate",
			IsLive: false);
	}
}
