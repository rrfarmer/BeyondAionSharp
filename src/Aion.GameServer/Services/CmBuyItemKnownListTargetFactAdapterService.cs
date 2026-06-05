using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum CmBuyItemKnownListTargetFactAdapterStatus
{
	MissingPlayer,
	UnknownWorldObject,
	NotKnownByPlayer,
	ResolvedFromKnownListFact,
	WorldObjectOnlyApproximation,
}

public sealed record CmBuyItemKnownListTargetFactAdapterPlan(
	CmBuyItemKnownListTargetFactAdapterStatus Status,
	int SellerObjectId,
	CmBuyItemRunTargetKind TargetKind,
	bool? IsKnownByPlayer,
	bool UsesWorldObjectOnlyApproximation,
	bool IsJavaKnownListParity,
	string JavaSource,
	bool IsLive);

public static class CmBuyItemKnownListTargetFactAdapterService
{
	public static CmBuyItemKnownListTargetFactAdapterPlan CreatePlan(
		Player? player,
		int sellerObjectId,
		object? worldObject,
		bool? isKnownByPlayer)
	{
		// Java parity: CM_BUY_ITEM.runImpl resolves target strictly via
		// player.getKnownList().getObject(sellerObjId), not global world lookup.
		if (player == null)
		{
			return CreatePlan(
				CmBuyItemKnownListTargetFactAdapterStatus.MissingPlayer,
				sellerObjectId,
				CmBuyItemRunTargetKind.Unknown,
				isKnownByPlayer,
				usesWorldObjectOnlyApproximation: false,
				isJavaKnownListParity: false,
				"CM_BUY_ITEM.runImpl -> player == null before known-list target lookup");
		}

		if (worldObject == null)
		{
			return CreatePlan(
				CmBuyItemKnownListTargetFactAdapterStatus.UnknownWorldObject,
				sellerObjectId,
				CmBuyItemRunTargetKind.Unknown,
				isKnownByPlayer,
				usesWorldObjectOnlyApproximation: false,
				isJavaKnownListParity: false,
				"CM_BUY_ITEM diagnostic path cannot classify target because the C# world object is unavailable");
		}

		if (isKnownByPlayer == false)
		{
			return CreatePlan(
				CmBuyItemKnownListTargetFactAdapterStatus.NotKnownByPlayer,
				sellerObjectId,
				CmBuyItemRunTargetKind.Unknown,
				isKnownByPlayer,
				usesWorldObjectOnlyApproximation: false,
				isJavaKnownListParity: true,
				"CM_BUY_ITEM.runImpl -> seller object exists globally but is not in player known-list");
		}

		var targetKind = worldObject switch
		{
			Player => CmBuyItemRunTargetKind.Player,
			IWorldNpcObject => CmBuyItemRunTargetKind.Npc,
			IWorldPetObject => CmBuyItemRunTargetKind.Pet,
			_ => CmBuyItemRunTargetKind.Other,
		};

		if (isKnownByPlayer == true)
		{
			return CreatePlan(
				CmBuyItemKnownListTargetFactAdapterStatus.ResolvedFromKnownListFact,
				sellerObjectId,
				targetKind,
				isKnownByPlayer,
				usesWorldObjectOnlyApproximation: false,
				isJavaKnownListParity: true,
				"CM_BUY_ITEM.runImpl -> player.getKnownList().getObject(sellerObjId) returned target");
		}

		return CreatePlan(
			CmBuyItemKnownListTargetFactAdapterStatus.WorldObjectOnlyApproximation,
			sellerObjectId,
			targetKind,
			isKnownByPlayer,
			usesWorldObjectOnlyApproximation: true,
			isJavaKnownListParity: false,
			"CM_BUY_ITEM diagnostic path classified target from C# world object only; Java known-list membership is not proven");
	}

	private static CmBuyItemKnownListTargetFactAdapterPlan CreatePlan(
		CmBuyItemKnownListTargetFactAdapterStatus status,
		int sellerObjectId,
		CmBuyItemRunTargetKind targetKind,
		bool? isKnownByPlayer,
		bool usesWorldObjectOnlyApproximation,
		bool isJavaKnownListParity,
		string javaSource)
	{
		return new CmBuyItemKnownListTargetFactAdapterPlan(
			status,
			sellerObjectId,
			targetKind,
			isKnownByPlayer,
			usesWorldObjectOnlyApproximation,
			isJavaKnownListParity,
			javaSource,
			IsLive: false);
	}
}
