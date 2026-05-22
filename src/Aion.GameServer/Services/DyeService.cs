using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class DyeService
{
	public static DyePlan CreateItemDyePlan(
		InventoryItem? targetItem,
		ItemTemplateSummary? targetSkinTemplate,
		ItemDyeActionInfo? action,
		DateTimeOffset now)
	{
		// Java parity: model/templates/item/actions/DyeAction.canAct item branch + dyeItem.
		if (action == null || targetItem == null)
			return DyePlan.Failed(DyeFailure.InvalidTarget);

		if (targetSkinTemplate?.IsItemDyePermitted != true)
			return DyePlan.Failed(DyeFailure.NotDyeable);

		var colorExpires = action.HasMinutes
			? now.ToUnixTimeSeconds() + action.Minutes * 60L
			: 0L;
		return new DyePlan(
			DyeFailure.None,
			action.Color,
			(int)Math.Min(int.MaxValue, colorExpires));
	}
}

public sealed record DyePlan(DyeFailure Failure, int? Color, int ColorExpires)
{
	public bool Succeeded => Failure == DyeFailure.None;

	public static DyePlan Failed(DyeFailure failure)
	{
		return new DyePlan(failure, null, 0);
	}
}

public enum DyeFailure
{
	None,
	InvalidTarget,
	NotDyeable,
}
