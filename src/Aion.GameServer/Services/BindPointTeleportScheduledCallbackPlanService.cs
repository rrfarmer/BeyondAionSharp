using Aion.GameServer.Network.Aion;

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
}

public sealed record BindPointTeleportScheduledCallbackPlan(
	BindPointTeleportScheduledCallbackPlanStatus Status,
	BindPointTeleportScheduledKinahPlan KinahPlan,
	BindPointTeleportCooldownPlan? CooldownPlan,
	BindPointTeleportFanoutPlan? CooldownFanoutPlan,
	BindPointTeleportFinalMovementPlan? FinalMovementPlan,
	IReadOnlyList<BindPointTeleportScheduledCallbackPlanStep> Steps,
	bool ShouldSendNotEnoughFee,
	bool ShouldStoreCooldown,
	bool ShouldBroadcastCooldown,
	bool ShouldScheduleFinalTeleport,
	bool ShouldTeleport,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportScheduledCallbackPlanService
{
	public static BindPointTeleportScheduledCallbackPlan CreatePlan(
		BindPointTeleportScheduledKinahPlan kinahPlan,
		BindPointTeleportCooldownPlan cooldownPlan,
		BindPointTeleportFanoutPlan cooldownFanoutPlan,
		BindPointTeleportFinalMovementPlan finalMovementPlan)
	{
		// Java parity: BindPointTeleportService.teleport scheduled SKILL_USE callback.
		// This only composes intent/order; no inventory, cooldown map, packet, scheduler, or movement side effects run here.
		if (!kinahPlan.ShouldContinueScheduledTeleport)
		{
			return new BindPointTeleportScheduledCallbackPlan(
				BindPointTeleportScheduledCallbackPlanStatus.StoppedNotEnoughKinah,
				kinahPlan,
				CooldownPlan: null,
				CooldownFanoutPlan: null,
				FinalMovementPlan: null,
				[
					BindPointTeleportScheduledCallbackPlanStep.TryDecreaseKinahFly,
					BindPointTeleportScheduledCallbackPlanStep.SendNotEnoughFeeAndReturn,
				],
				ShouldSendNotEnoughFee: kinahPlan.ShouldSendNotEnoughFee,
				ShouldStoreCooldown: false,
				ShouldBroadcastCooldown: false,
				ShouldScheduleFinalTeleport: false,
				ShouldTeleport: false,
				"BindPointTeleportService.teleport scheduled task -> tryDecreaseKinah failed -> send STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE and return before cooldown/fanout/final teleport",
				IsLive: false);
		}

		var shouldTeleport = finalMovementPlan.ShouldTeleport;
		return new BindPointTeleportScheduledCallbackPlan(
			shouldTeleport
				? BindPointTeleportScheduledCallbackPlanStatus.ReadyWithMovement
				: BindPointTeleportScheduledCallbackPlanStatus.ReadyWithoutMovement,
			kinahPlan,
			cooldownPlan,
			cooldownFanoutPlan,
			finalMovementPlan,
			[
				BindPointTeleportScheduledCallbackPlanStep.TryDecreaseKinahFly,
				BindPointTeleportScheduledCallbackPlanStep.AddCooldown,
				BindPointTeleportScheduledCallbackPlanStep.BroadcastCooldown,
				BindPointTeleportScheduledCallbackPlanStep.ScheduleFinalTeleport,
				BindPointTeleportScheduledCallbackPlanStep.CheckFinalMovementGate,
				.. shouldTeleport
					? [BindPointTeleportScheduledCallbackPlanStep.CreateFinalMovementIntent]
					: Array.Empty<BindPointTeleportScheduledCallbackPlanStep>(),
			],
			ShouldSendNotEnoughFee: false,
			ShouldStoreCooldown: cooldownPlan.ShouldStoreCooldown,
			ShouldBroadcastCooldown: cooldownFanoutPlan.Packet is GameServerPacket,
			ShouldScheduleFinalTeleport: true,
			ShouldTeleport: shouldTeleport,
			"BindPointTeleportService.teleport scheduled task -> tryDecreaseKinah succeeded -> addCooldown -> broadcast action 3 -> schedule 1000ms final teleport gate",
			IsLive: false);
	}
}
