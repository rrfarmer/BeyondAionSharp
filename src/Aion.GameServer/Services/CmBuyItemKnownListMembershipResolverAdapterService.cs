using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum CmBuyItemKnownListMembershipResolverAdapterStatus
{
	MissingPlayer,
	MissingMembershipService,
	UnsupportedTargetKind,
	KnownPlayerTarget,
	UnknownPlayerTarget,
}

public sealed record CmBuyItemKnownListMembershipResolverAdapterPlan(
	CmBuyItemKnownListMembershipResolverAdapterStatus Status,
	int SellerObjectId,
	bool? IsKnownByPlayer,
	int SnapshotEntryCount,
	bool UsesPlayerKnownListMembershipSnapshot,
	bool IsJavaKnownListParity,
	string JavaSource,
	bool IsLive);

public static class CmBuyItemKnownListMembershipResolverAdapterService
{
	public static Func<Player, int, object?, bool?> CreateResolver(PlayerKnownListMembershipService membershipService) =>
		(player, sellerObjectId, worldObject) => CreatePlan(
			player,
			sellerObjectId,
			worldObject,
			membershipService).IsKnownByPlayer;

	public static CmBuyItemKnownListMembershipResolverAdapterPlan CreatePlan(
		Player? player,
		int sellerObjectId,
		object? worldObject,
		PlayerKnownListMembershipService? membershipService)
	{
		// Java parity: KnownList.getObject covers every VisibleObject. The current C#
		// membership snapshot only tracks player objects, so NPC/pet/object targets
		// deliberately return an unknown fact instead of a false rejection.
		if (player == null)
		{
			return CreatePlan(
				CmBuyItemKnownListMembershipResolverAdapterStatus.MissingPlayer,
				sellerObjectId,
				isKnownByPlayer: null,
				snapshotEntryCount: 0,
				usesPlayerKnownListMembershipSnapshot: false,
				"CM_BUY_ITEM known-list resolver adapter cannot read membership without active player");
		}

		if (membershipService == null)
		{
			return CreatePlan(
				CmBuyItemKnownListMembershipResolverAdapterStatus.MissingMembershipService,
				sellerObjectId,
				isKnownByPlayer: null,
				snapshotEntryCount: 0,
				usesPlayerKnownListMembershipSnapshot: false,
				"CM_BUY_ITEM known-list resolver adapter has no PlayerKnownListMembershipService");
		}

		if (worldObject is not Player)
		{
			return CreatePlan(
				CmBuyItemKnownListMembershipResolverAdapterStatus.UnsupportedTargetKind,
				sellerObjectId,
				isKnownByPlayer: null,
				snapshotEntryCount: 0,
				usesPlayerKnownListMembershipSnapshot: false,
				"KnownList.getObject supports all VisibleObject types; C# PlayerKnownListMembershipService only tracks player targets");
		}

		var snapshot = membershipService.GetSnapshot(player.ObjectId);
		var isKnown = snapshot.KnownPlayerObjectIds.Contains(sellerObjectId);
		return CreatePlan(
			isKnown
				? CmBuyItemKnownListMembershipResolverAdapterStatus.KnownPlayerTarget
				: CmBuyItemKnownListMembershipResolverAdapterStatus.UnknownPlayerTarget,
			sellerObjectId,
			isKnown,
			snapshot.Entries.Count,
			usesPlayerKnownListMembershipSnapshot: true,
			"KnownList.getObject player-target membership approximated from PlayerKnownListMembershipService snapshot");
	}

	private static CmBuyItemKnownListMembershipResolverAdapterPlan CreatePlan(
		CmBuyItemKnownListMembershipResolverAdapterStatus status,
		int sellerObjectId,
		bool? isKnownByPlayer,
		int snapshotEntryCount,
		bool usesPlayerKnownListMembershipSnapshot,
		string javaSource)
	{
		return new CmBuyItemKnownListMembershipResolverAdapterPlan(
			status,
			sellerObjectId,
			isKnownByPlayer,
			snapshotEntryCount,
			usesPlayerKnownListMembershipSnapshot,
			IsJavaKnownListParity: false,
			javaSource,
			IsLive: false);
	}
}
