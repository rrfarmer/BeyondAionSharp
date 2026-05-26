using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum BindPointTeleportOperationPlanStatus
{
	ReadyToSchedule,
	InvalidHotspot,
	RequirementsFailed,
}

public enum BindPointTeleportOperationStep
{
	AuditInvalidHotspot,
	SendNoRoute,
	BroadcastStart,
	ScheduleSkillUseTask,
	TryDecreaseKinahFly,
	SendNotEnoughFeeIfScheduledKinahDecreaseFails,
	AddCooldown,
	BroadcastCooldown,
	ScheduleFinalTeleport,
}

public sealed record BindPointTeleportOperationPlan(
	BindPointTeleportOperationPlanStatus Status,
	bool CanSchedule,
	int LocId,
	long? RequiredPrice,
	BindPointTeleportRequirementStatus? RequirementStatus,
	bool ShouldWarnPriceMismatch,
	string? SystemMessage,
	string? AuditMessage,
	IReadOnlyList<BindPointTeleportOperationStep> Steps,
	IReadOnlyList<SmBindPointTeleport> PacketIntents,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportOperationPlanService
{
	private const int StartDelayMilliseconds = 10_000;
	private const int FinalTeleportDelayMilliseconds = 1_000;
	private const int CooldownSeconds = 600;

	public static BindPointTeleportOperationPlan CreatePlan(
		int playerObjectId,
		int locId,
		bool hotspotExists,
		BindPointTeleportPricePlan? pricePlan,
		BindPointTeleportRequirementsPlan? requirementsPlan)
	{
		// Java parity: services/teleport/BindPointTeleportService.teleport.
		// This composes non-live operation intent only; no PacketSendUtility, ThreadPoolManager, cooldown map, or inventory mutation runs here.
		if (!hotspotExists)
		{
			return new BindPointTeleportOperationPlan(
				BindPointTeleportOperationPlanStatus.InvalidHotspot,
				CanSchedule: false,
				locId,
				RequiredPrice: null,
				RequirementStatus: null,
				ShouldWarnPriceMismatch: false,
				"STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE",
				$"Tried to use invalid hotspot teleport to locId {locId}",
				[BindPointTeleportOperationStep.AuditInvalidHotspot, BindPointTeleportOperationStep.SendNoRoute],
				[],
				"BindPointTeleportService.teleport -> invalid hotspot",
				IsLive: false);
		}

		if (requirementsPlan == null || !requirementsPlan.CanTeleport)
		{
			return new BindPointTeleportOperationPlan(
				BindPointTeleportOperationPlanStatus.RequirementsFailed,
				CanSchedule: false,
				locId,
				pricePlan?.FinalPrice,
				requirementsPlan?.Status,
				pricePlan?.ShouldWarnPriceMismatch ?? false,
				requirementsPlan?.SystemMessage,
				requirementsPlan?.AuditMessage,
				[],
				[],
				"BindPointTeleportService.teleport -> checkRequirements false",
				IsLive: false);
		}

		return new BindPointTeleportOperationPlan(
			BindPointTeleportOperationPlanStatus.ReadyToSchedule,
			CanSchedule: true,
			locId,
			pricePlan?.FinalPrice,
			requirementsPlan.Status,
			pricePlan?.ShouldWarnPriceMismatch ?? false,
			SystemMessage: null,
			AuditMessage: null,
			[
				BindPointTeleportOperationStep.BroadcastStart,
				BindPointTeleportOperationStep.ScheduleSkillUseTask,
				BindPointTeleportOperationStep.TryDecreaseKinahFly,
				BindPointTeleportOperationStep.SendNotEnoughFeeIfScheduledKinahDecreaseFails,
				BindPointTeleportOperationStep.AddCooldown,
				BindPointTeleportOperationStep.BroadcastCooldown,
				BindPointTeleportOperationStep.ScheduleFinalTeleport,
			],
			[
				SmBindPointTeleport.Start(playerObjectId, locId),
				SmBindPointTeleport.Cooldown(playerObjectId, locId, CooldownSeconds),
			],
			$"BindPointTeleportService.teleport -> broadcast action 1 -> schedule {StartDelayMilliseconds}ms skill task -> tryDecreaseKinah(DEC_KINAH_FLY) -> add {CooldownSeconds}s cooldown -> broadcast action 3 -> schedule {FinalTeleportDelayMilliseconds}ms teleport",
			IsLive: false);
	}
}
