using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum CubeExpandType
{
	Npc = 1,
	Item = 2,
	Quest = 3,
}

public enum CubeExpandNotificationPlanStatus
{
	PlanCreated,
}

public sealed record CubeExpandNotificationPlan(
	CubeExpandNotificationPlanStatus Status,
	CubeExpandType ExpandType,
	SmSystemMessage ExpandedMessage,
	int SlotsAdded,
	bool ShouldSendToPlayer,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class CubeExpandNotificationPlanService
{
	// Java parity: services/CubeExpandService.expand always passes 9 as slot count.
	public const int SlotsPerExpansion = 9;

	public static CubeExpandNotificationPlan CreatePlan(CubeExpandType expandType)
	{
		// Java parity: services/CubeExpandService.expand sends STR_EXTEND_INVENTORY_SIZE_EXTENDED(9).
		// Live player.getCommonData().setNpcExpands/setItemExpands/setQuestExpands, player.setCubeLimit(),
		// and SM_CUBE_UPDATE.cubeSize dispatch are deferred.
		return new CubeExpandNotificationPlan(
			CubeExpandNotificationPlanStatus.PlanCreated,
			expandType,
			SmSystemMessage.InventorySizeExtended(SlotsPerExpansion),
			SlotsAdded: SlotsPerExpansion,
			ShouldSendToPlayer: true,
			$"CubeExpandService.expand(type={(int)expandType}) -> STR_EXTEND_INVENTORY_SIZE_EXTENDED(9) -> set{expandType}Expands+1 [live/deferred] -> setCubeLimit [live/deferred] -> SM_CUBE_UPDATE [live/deferred]"
		);
	}
}
