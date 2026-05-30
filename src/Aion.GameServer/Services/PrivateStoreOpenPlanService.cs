using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PrivateStoreOpenPlanStatus
{
	PlanCreated,
	BlockedEmptyStoreMessage,
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
		if (string.IsNullOrEmpty(storeMessage))
		{
			return new PrivateStoreOpenPlan(
				PrivateStoreOpenPlanStatus.BlockedEmptyStoreMessage,
				playerObjectId,
				storeMessage,
				PrivateStoreNamePacket: null,
				ShouldBroadcastStoreName: false,
				"PrivateStoreService.openPrivateStore -> store message is empty; SM_PRIVATE_STORE_NAME not broadcast"
			);
		}

		return new PrivateStoreOpenPlan(
			PrivateStoreOpenPlanStatus.PlanCreated,
			playerObjectId,
			storeMessage,
			new SmPrivateStoreName(playerObjectId, storeMessage),
			ShouldBroadcastStoreName: true,
			"PrivateStoreService.openPrivateStore -> activePlayer.getStore().setStoreMessage(name) [live/deferred] -> broadcastPacket(SM_PRIVATE_STORE_NAME, true)"
		);
	}
}
