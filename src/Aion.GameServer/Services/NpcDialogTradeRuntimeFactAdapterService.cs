namespace Aion.GameServer.Services;

public sealed record NpcDialogTradeRuntimeFactAdapterInput(
	int PlayerObjectId,
	int PlayerLegionId = 0,
	int? PlayerLegionLevel = null,
	int? VendorBuyModifier = null);

public sealed record NpcDialogTradeRuntimeFactAdapterPlan(
	int PlayerObjectId,
	int PlayerLegionId,
	int PlayerLegionLevel,
	int VendorBuyModifier,
	string LegionLevelSource,
	string VendorBuyModifierSource,
	string JavaSource,
	string Notes,
	bool IsLive = false)
{
	public NpcDialogTradeListFactAdapterInput ToTradeListFactInput(int npcId)
	{
		return new NpcDialogTradeListFactAdapterInput(
			npcId,
			PlayerLegionLevel,
			VendorBuyModifier);
	}

	public NpcDialogLimitedItemFactAdapterInput ToLimitedItemFactInput(int npcId)
	{
		return new NpcDialogLimitedItemFactAdapterInput(npcId, PlayerObjectId);
	}
}

public static class NpcDialogTradeRuntimeFactAdapterService
{
	public const int JavaNoLegionLevel = 0;
	public const int JavaDefaultVendorBuyModifier = 100;

	public static NpcDialogTradeRuntimeFactAdapterPlan CreatePlan(NpcDialogTradeRuntimeFactAdapterInput input)
	{
		// Java parity breadcrumbs:
		// - DialogService.onDialogSelect BUY calculates legionLevel from
		//   player.getLegion() == null ? 0 : player.getLegion().getLegionLevel().
		// - DialogService passes PricesService.getVendorBuyModifier() * tradeModifier / 100
		//   into SM_TRADELIST. The C# port does not yet have live LegionService or
		//   PricesConfig-backed runtime plumbing at this socket boundary.
		var playerLegionLevel = input.PlayerLegionLevel ?? JavaNoLegionLevel;
		var vendorBuyModifier = input.VendorBuyModifier ?? JavaDefaultVendorBuyModifier;

		return new NpcDialogTradeRuntimeFactAdapterPlan(
			input.PlayerObjectId,
			input.PlayerLegionId,
			playerLegionLevel,
			vendorBuyModifier,
			input.PlayerLegionLevel.HasValue
				? "Injected runtime value"
				: "Staged default: Player.getLegion() unavailable, Java no-legion fallback",
			input.VendorBuyModifier.HasValue
				? "Injected runtime value"
				: "Staged default: PricesConfig.VENDOR_BUY_MODIFIER not wired",
			"DialogService.onDialogSelect BUY -> Player.getLegion().getLegionLevel + PricesService.getVendorBuyModifier",
			input.PlayerLegionLevel.HasValue && input.VendorBuyModifier.HasValue
				? "Values were supplied by the caller; live lookup ownership remains outside this adapter."
				: "Non-live fact seam only. Replace staged defaults after LegionService and PricesConfig runtime sources are ported.",
			IsLive: false);
	}
}
