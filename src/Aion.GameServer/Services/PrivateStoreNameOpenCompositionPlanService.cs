using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public enum PrivateStoreNameOpenCompositionPlanStatus
{
	MissingStorePrecondition,
	OpenPlanCreated,
}

public sealed record PrivateStoreNameOpenCompositionContext(
	int PlayerObjectId,
	bool StoreIsOpen);

public sealed record PrivateStoreNameOpenCompositionPlan(
	CmPrivateStoreName Packet,
	PrivateStoreNameOpenCompositionPlanStatus Status,
	PrivateStoreNameOpenCompositionContext Context,
	PrivateStoreOpenPlan? OpenPlan,
	bool WouldSetStoreMessage,
	bool WouldBroadcastStoreName,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class PrivateStoreNameOpenCompositionPlanService
{
	public static PrivateStoreNameOpenCompositionPlan CreateDisabledPlan(
		CmPrivateStoreName packet,
		PrivateStoreNameOpenCompositionContext context)
	{
		// Java parity: network/aion/clientpackets/CM_PRIVATE_STORE_NAME.runImpl.
		// Java assumes activePlayer.getStore() exists; this disabled boundary records
		// the missing-store precondition instead of dereferencing live state.
		if (!context.StoreIsOpen)
		{
			return new PrivateStoreNameOpenCompositionPlan(
				packet,
				PrivateStoreNameOpenCompositionPlanStatus.MissingStorePrecondition,
				context,
				OpenPlan: null,
				WouldSetStoreMessage: false,
				WouldBroadcastStoreName: false,
				"CM_PRIVATE_STORE_NAME.runImpl -> PrivateStoreService.openPrivateStore(activePlayer, name); Java would require activePlayer.getStore() before setting the message");
		}

		var openPlan = PrivateStoreOpenPlanService.CreatePlan(context.PlayerObjectId, packet.StoreName);
		return new PrivateStoreNameOpenCompositionPlan(
			packet,
			PrivateStoreNameOpenCompositionPlanStatus.OpenPlanCreated,
			context,
			openPlan,
			WouldSetStoreMessage: true,
			WouldBroadcastStoreName: openPlan.ShouldBroadcastStoreName,
			"CM_PRIVATE_STORE_NAME.runImpl -> PrivateStoreService.openPrivateStore(activePlayer, name), with live store-message mutation and broadcast disabled");
	}
}
