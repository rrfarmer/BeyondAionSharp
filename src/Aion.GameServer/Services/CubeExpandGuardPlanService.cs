using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum CubeExpandGuardPlanStatus
{
	CanExpand,
	BlockedNegativeExpansionCount,
	BlockedAtLimit,
	BlockedTicketLevelAlreadyReached,
}

public sealed record CubeExpandGuardPlan(
	CubeExpandGuardPlanStatus Status,
	int NpcExpands,
	int QuestExpands,
	int ItemExpands,
	int NewExpansionCount,
	SmSystemMessage? DenialMessage,
	bool CanExpand,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class CubeExpandGuardPlanService
{
	public static CubeExpandGuardPlan CreateCanExpandPlan(
		int npcExpands,
		int questExpands,
		int itemExpands,
		int cubeExpansionLimit)
	{
		// Java parity: services/CubeExpandService.canExpand.
		var newExpansions = npcExpands + questExpands + itemExpands + 1;

		if (newExpansions < 0)
		{
			return new CubeExpandGuardPlan(
				CubeExpandGuardPlanStatus.BlockedNegativeExpansionCount,
				npcExpands, questExpands, itemExpands, newExpansions,
				DenialMessage: null,
				CanExpand: false,
				"CubeExpandService.canExpand -> newExpansions < 0 -> return false"
			);
		}

		if (newExpansions > cubeExpansionLimit)
		{
			return new CubeExpandGuardPlan(
				CubeExpandGuardPlanStatus.BlockedAtLimit,
				npcExpands, questExpands, itemExpands, newExpansions,
				SmSystemMessage.InventoryCantExtendMore(),
				CanExpand: false,
				$"CubeExpandService.canExpand -> newExpansions({newExpansions}) > limit({cubeExpansionLimit}) -> STR_EXTEND_INVENTORY_CANT_EXTEND_MORE"
			);
		}

		return new CubeExpandGuardPlan(
			CubeExpandGuardPlanStatus.CanExpand,
			npcExpands, questExpands, itemExpands, newExpansions,
			DenialMessage: null,
			CanExpand: true,
			$"CubeExpandService.canExpand -> newExpansions={newExpansions} <= limit={cubeExpansionLimit} -> return true"
		);
	}

	public static CubeExpandGuardPlan CreateCanExpandByTicketPlan(
		int npcExpands,
		int questExpands,
		int itemExpands,
		int cubeExpansionLimit,
		int ticketLevel)
	{
		// Java parity: services/CubeExpandService.canExpandByTicket.
		var baseGuard = CreateCanExpandPlan(npcExpands, questExpands, itemExpands, cubeExpansionLimit);
		if (!baseGuard.CanExpand)
			return baseGuard;

		if (itemExpands >= ticketLevel)
		{
			return new CubeExpandGuardPlan(
				CubeExpandGuardPlanStatus.BlockedTicketLevelAlreadyReached,
				npcExpands, questExpands, itemExpands, baseGuard.NewExpansionCount,
				SmSystemMessage.InventoryCantExtendMore(),
				CanExpand: false,
				$"CubeExpandService.canExpandByTicket -> itemExpands({itemExpands}) >= ticketLevel({ticketLevel}) -> STR_EXTEND_INVENTORY_CANT_EXTEND_MORE"
			);
		}

		return baseGuard;
	}
}
