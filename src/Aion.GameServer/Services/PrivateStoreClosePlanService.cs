using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PrivateStoreClosePlanStatus
{
	PlanCreated,
	SkippedNoStoreOpen,
}

public sealed record PrivateStoreClosePlan(
	PrivateStoreClosePlanStatus Status,
	int PlayerObjectId,
	SmEmotion? ClosePrivateShopEmotionPacket,
	bool ShouldBroadcastEmotion,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PrivateStoreClosePlanService
{
	public static PrivateStoreClosePlan CreatePlan(int playerObjectId, int playerCreatureState, bool storeIsOpen)
	{
		// Java parity: services/PrivateStoreService.closePrivateStore.
		// Live player.setStore(null), unsetState(PRIVATE_SHOP), setState(ACTIVE) are deferred.
		if (!storeIsOpen)
		{
			return new PrivateStoreClosePlan(
				PrivateStoreClosePlanStatus.SkippedNoStoreOpen,
				playerObjectId,
				ClosePrivateShopEmotionPacket: null,
				ShouldBroadcastEmotion: false,
				"PrivateStoreService.closePrivateStore -> player.getStore() == null -> return"
			);
		}

		var emotionPacket = new SmEmotion(
			playerObjectId,
			EmotionType.ClosePrivateShop,
			creatureState: playerCreatureState,
			movementSpeed: 0,
			baseAttackSpeed: 0,
			currentAttackSpeed: 0);

		return new PrivateStoreClosePlan(
			PrivateStoreClosePlanStatus.PlanCreated,
			playerObjectId,
			emotionPacket,
			ShouldBroadcastEmotion: true,
			"PrivateStoreService.closePrivateStore -> setStore(null) [live/deferred] -> unsetState(PRIVATE_SHOP) [live/deferred] -> setState(ACTIVE) [live/deferred] -> broadcastPacket(SM_EMOTION CLOSE_PRIVATESHOP, true)"
		);
	}
}
