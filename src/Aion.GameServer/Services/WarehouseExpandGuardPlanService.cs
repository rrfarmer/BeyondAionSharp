using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum WarehouseExpandGuardPlanStatus
{
	CanExpand,
	BlockedNegativeExpansionCount,
	BlockedAtLimit,
	BlockedTicketLevelAlreadyReached,
}

public sealed record WarehouseExpandGuardPlan(
	WarehouseExpandGuardPlanStatus Status,
	int WarehouseExpansions,
	int NewExpansionCount,
	SmSystemMessage? DenialMessage,
	bool CanExpand,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class WarehouseExpandGuardPlanService
{
	// Java parity: services/WarehouseService.MAX_EXPAND.
	public const int MaxExpand = 11;

	// Java parity: services/WarehouseService.getCompletedWhQuests quest IDs.
	public static readonly IReadOnlyList<int> WarehouseQuestIds = [1987, 2985];

	public static WarehouseExpandGuardPlan CreateCanExpandPlan(
		int warehouseExpansions,
		int maxExpand = MaxExpand)
	{
		// Java parity: services/WarehouseService.canExpand.
		var newExpansions = warehouseExpansions + 1;

		if (newExpansions < 0)
		{
			return new WarehouseExpandGuardPlan(
				WarehouseExpandGuardPlanStatus.BlockedNegativeExpansionCount,
				warehouseExpansions, newExpansions,
				DenialMessage: null,
				CanExpand: false,
				"WarehouseService.canExpand -> newExpansions < 0 -> return false"
			);
		}

		if (newExpansions > maxExpand)
		{
			return new WarehouseExpandGuardPlan(
				WarehouseExpandGuardPlanStatus.BlockedAtLimit,
				warehouseExpansions, newExpansions,
				SmSystemMessage.WarehouseCantExtendMore(),
				CanExpand: false,
				$"WarehouseService.canExpand -> newExpansions({newExpansions}) > MAX_EXPAND({maxExpand}) -> STR_EXTEND_CHAR_WAREHOUSE_CANT_EXTEND_MORE"
			);
		}

		return new WarehouseExpandGuardPlan(
			WarehouseExpandGuardPlanStatus.CanExpand,
			warehouseExpansions, newExpansions,
			DenialMessage: null,
			CanExpand: true,
			$"WarehouseService.canExpand -> newExpansions={newExpansions} <= MAX_EXPAND={maxExpand} -> return true"
		);
	}

	public static WarehouseExpandGuardPlan CreateCanExpandByTicketPlan(
		int warehouseExpansions,
		int whBonusExpands,
		int completedWhQuests,
		int ticketLevel,
		int maxExpand = MaxExpand)
	{
		// Java parity: services/WarehouseService.canExpandByTicket.
		// completedWhQuests: count of completed quests 1987 and 2985 (0, 1, or 2).
		var baseGuard = CreateCanExpandPlan(warehouseExpansions, maxExpand);
		if (!baseGuard.CanExpand)
			return baseGuard;

		var effectiveBonusExpands = whBonusExpands - completedWhQuests;
		if (effectiveBonusExpands >= ticketLevel)
		{
			return new WarehouseExpandGuardPlan(
				WarehouseExpandGuardPlanStatus.BlockedTicketLevelAlreadyReached,
				warehouseExpansions, baseGuard.NewExpansionCount,
				SmSystemMessage.WarehouseCantExtendMore(),
				CanExpand: false,
				$"WarehouseService.canExpandByTicket -> whBonusExpands({whBonusExpands})-completedQuests({completedWhQuests})={effectiveBonusExpands} >= ticketLevel({ticketLevel}) -> STR_EXTEND_CHAR_WAREHOUSE_CANT_EXTEND_MORE"
			);
		}

		return baseGuard;
	}
}
