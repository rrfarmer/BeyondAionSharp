using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum WarehouseExpandType
{
	Npc,
	BonusTicket,
}

public enum WarehouseExpandNotificationPlanStatus
{
	PlanCreated,
}

public sealed record WarehouseExpandNotificationPlan(
	WarehouseExpandNotificationPlanStatus Status,
	WarehouseExpandType ExpandType,
	SmSystemMessage ExpandedMessage,
	int SlotsAdded,
	bool ShouldSendToPlayer,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class WarehouseExpandNotificationPlanService
{
	// Java parity: services/WarehouseService.expand hardcodes STR_EXTEND_CHAR_WAREHOUSE_SIZE_EXTENDED(8).
	public const int SlotsPerExpansion = 8;

	public static WarehouseExpandNotificationPlan CreatePlan(WarehouseExpandType expandType)
	{
		// Java parity: services/WarehouseService.expand sends STR_EXTEND_CHAR_WAREHOUSE_SIZE_EXTENDED(8).
		// Live pcd.setWhNpcExpands/setWhBonusExpands, player.setWarehouseLimit, and sendWarehouseInfo are deferred.
		return new WarehouseExpandNotificationPlan(
			WarehouseExpandNotificationPlanStatus.PlanCreated,
			expandType,
			SmSystemMessage.WarehouseSizeExtended(SlotsPerExpansion),
			SlotsAdded: SlotsPerExpansion,
			ShouldSendToPlayer: true,
			$"WarehouseService.expand(isNpcExpand={expandType == WarehouseExpandType.Npc}) -> STR_EXTEND_CHAR_WAREHOUSE_SIZE_EXTENDED(8) -> set{expandType}Expands+1 [live/deferred] -> setWarehouseLimit [live/deferred] -> sendWarehouseInfo [live/deferred]"
		);
	}
}
