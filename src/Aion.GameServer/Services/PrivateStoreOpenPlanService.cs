using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PrivateStoreOpenPlanStatus
{
	PlanCreated,
}

public sealed record PrivateStoreOpenPlan(
	PrivateStoreOpenPlanStatus Status,
	int PlayerObjectId,
	string? StoreMessage,
	SmPrivateStoreName? PrivateStoreNamePacket,
	bool ShouldBroadcastStoreName,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PrivateStoreOpenPlanService
{
	public static PrivateStoreOpenPlan CreatePlan(int playerObjectId, string? storeMessage)
	{
		// Java parity: services/PrivateStoreService.openPrivateStore.
		// Sets store message then broadcasts SM_PRIVATE_STORE_NAME to all visible players including self.
		// Live player.getStore().setStoreMessage and broadcastPacket dispatch are deferred.
		var effectiveStoreMessage = storeMessage ?? string.Empty;

		return new PrivateStoreOpenPlan(
			PrivateStoreOpenPlanStatus.PlanCreated,
			playerObjectId,
			effectiveStoreMessage,
			new SmPrivateStoreName(playerObjectId, effectiveStoreMessage),
			ShouldBroadcastStoreName: true,
			"PrivateStoreService.openPrivateStore -> activePlayer.getStore().setStoreMessage(name) [live/deferred] -> broadcastPacket(SM_PRIVATE_STORE_NAME, true); empty/null names serialize as PrivateStore.getStoreMessage() empty string"
		);
	}
}
