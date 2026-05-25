using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemPurificationLiveMutationService
{
	public static ItemPurificationLiveMutationResult Apply(
		Player? player,
		ItemPurificationApplicationPlan? applicationPlan,
		int npcExpands,
		int questExpands,
		int itemExpands,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: services/item/ItemPurificationService.decreaseMaterials mutates Storage,
		// spends AP through AbyssPointsService.addAp, preserves the current negative-kinah quirk,
		// removes the base item, then upgradeItem adds the target item. Persistence, send fanout,
		// quest notifications, and rollback semantics are intentionally outside this boundary.
		if (player == null)
			return ItemPurificationLiveMutationResult.Failed(ItemPurificationLiveMutationStatus.MissingPlayer);
		if (applicationPlan == null)
			return ItemPurificationLiveMutationResult.Failed(ItemPurificationLiveMutationStatus.MissingApplicationPlan);
		if (!applicationPlan.Succeeded)
			return ItemPurificationLiveMutationResult.Failed(ItemPurificationLiveMutationStatus.ApplicationPlanNotReady);

		var mutationPreview = ItemPurificationMutationSnapshotService.CreatePreview(
			player.InventoryItems,
			applicationPlan,
			npcExpands,
			questExpands,
			itemExpands);
		if (!mutationPreview.Succeeded)
		{
			return new ItemPurificationLiveMutationResult(
				ItemPurificationLiveMutationStatus.MutationSnapshotNotReady,
				mutationPreview,
				AbyssPointsPlan: null,
				AppliedInventoryItems: player.InventoryItems);
		}

		player.InventoryItems = mutationPreview.PostMutationInventoryItems;
		var abyssPointsPlan = applicationPlan.AbyssPointsToSpend > 0
			? AbyssPointsService.AddAp(player, -applicationPlan.AbyssPointsToSpend, abyssPointsOptions)
			: null;

		return new ItemPurificationLiveMutationResult(
			ItemPurificationLiveMutationStatus.Ready,
			mutationPreview,
			abyssPointsPlan,
			player.InventoryItems);
	}
}

public sealed record ItemPurificationLiveMutationResult(
	ItemPurificationLiveMutationStatus Status,
	ItemPurificationMutationSnapshotPlan? MutationPreview,
	AbyssPointsAddPlan? AbyssPointsPlan,
	IReadOnlyList<InventoryItem> AppliedInventoryItems)
{
	public bool Succeeded => Status == ItemPurificationLiveMutationStatus.Ready;

	public static ItemPurificationLiveMutationResult Failed(ItemPurificationLiveMutationStatus status)
	{
		return new ItemPurificationLiveMutationResult(
			status,
			MutationPreview: null,
			AbyssPointsPlan: null,
			AppliedInventoryItems: Array.Empty<InventoryItem>());
	}
}

public enum ItemPurificationLiveMutationStatus
{
	Ready,
	MissingPlayer,
	MissingApplicationPlan,
	ApplicationPlanNotReady,
	MutationSnapshotNotReady,
}
