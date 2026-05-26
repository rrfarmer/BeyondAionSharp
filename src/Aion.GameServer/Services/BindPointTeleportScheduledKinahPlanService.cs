namespace Aion.GameServer.Services;

public enum BindPointTeleportScheduledKinahPlanStatus
{
	DecrementReady,
	NotEnoughKinah,
}

public enum BindPointTeleportScheduledKinahPlanStep
{
	TryDecreaseKinahFly,
	SendNotEnoughFee,
	ContinueCooldownAndTeleportFlow,
}

public sealed record BindPointTeleportScheduledKinahPlan(
	BindPointTeleportScheduledKinahPlanStatus Status,
	long RequiredPrice,
	long CurrentKinah,
	long? RemainingKinah,
	bool ShouldDecreaseKinah,
	bool ShouldSendNotEnoughFee,
	bool ShouldContinueScheduledTeleport,
	string ItemUpdateTypeName,
	int ItemUpdateTypeMask,
	string? SystemMessage,
	IReadOnlyList<BindPointTeleportScheduledKinahPlanStep> Steps,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportScheduledKinahPlanService
{
	public const string DecKinahFlyUpdateTypeName = "ItemPacketService.ItemUpdateType.DEC_KINAH_FLY";
	public const int DecKinahFlyUpdateTypeMask = 0x4B;
	public const string NotEnoughFeeSystemMessage = "STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE";

	public static BindPointTeleportScheduledKinahPlan CreatePlan(long requiredPrice, long currentKinah)
	{
		// Java parity: BindPointTeleportService.teleport scheduled SKILL_USE task first tries
		// player.getInventory().tryDecreaseKinah(price, ItemUpdateType.DEC_KINAH_FLY).
		if (currentKinah < requiredPrice)
		{
			return new BindPointTeleportScheduledKinahPlan(
				BindPointTeleportScheduledKinahPlanStatus.NotEnoughKinah,
				requiredPrice,
				currentKinah,
				RemainingKinah: null,
				ShouldDecreaseKinah: false,
				ShouldSendNotEnoughFee: true,
				ShouldContinueScheduledTeleport: false,
				DecKinahFlyUpdateTypeName,
				DecKinahFlyUpdateTypeMask,
				NotEnoughFeeSystemMessage,
				[
					BindPointTeleportScheduledKinahPlanStep.TryDecreaseKinahFly,
					BindPointTeleportScheduledKinahPlanStep.SendNotEnoughFee,
				],
				"BindPointTeleportService.teleport scheduled task -> if (!tryDecreaseKinah(price, DEC_KINAH_FLY)) send STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE and return",
				IsLive: false);
		}

		return new BindPointTeleportScheduledKinahPlan(
			BindPointTeleportScheduledKinahPlanStatus.DecrementReady,
			requiredPrice,
			currentKinah,
			RemainingKinah: currentKinah - requiredPrice,
			ShouldDecreaseKinah: true,
			ShouldSendNotEnoughFee: false,
			ShouldContinueScheduledTeleport: true,
			DecKinahFlyUpdateTypeName,
			DecKinahFlyUpdateTypeMask,
			SystemMessage: null,
			[
				BindPointTeleportScheduledKinahPlanStep.TryDecreaseKinahFly,
				BindPointTeleportScheduledKinahPlanStep.ContinueCooldownAndTeleportFlow,
			],
			"BindPointTeleportService.teleport scheduled task -> tryDecreaseKinah(price, DEC_KINAH_FLY) succeeded before addCooldown/broadcast/final teleport",
			IsLive: false);
	}
}
