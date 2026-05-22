using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class InventoryExpansionService
{
	public const int CubeSlotsPerExpansion = 9;
	public const int WarehouseSlotsPerExpansion = 8;
	private const int WarehouseExpansionLimit = 11;
	private static readonly int[] WarehouseQuestIds = [1987, 2985];

	public static InventoryExpansionPlan CreatePlan(
		Player player,
		ItemExpandInventoryActionInfo? action,
		int cubeExpansionLimit)
	{
		// Java parity: model/templates/item/actions/ExpandInventoryAction.canAct dispatches to
		// CubeExpandService.canExpandByTicket or WarehouseService.canExpandByTicket.
		if (action == null || action.Level <= 0)
			return InventoryExpansionPlan.Failed(InventoryExpansionFailure.MissingAction);

		return action.Storage switch
		{
			"CUBE" => CreateCubePlan(player, action.Level, cubeExpansionLimit),
			"WAREHOUSE" => CreateWarehousePlan(player, action.Level),
			_ => InventoryExpansionPlan.Failed(InventoryExpansionFailure.MissingAction),
		};
	}

	private static InventoryExpansionPlan CreateCubePlan(Player player, int ticketLevel, int cubeExpansionLimit)
	{
		if (!CanExpandCube(player, cubeExpansionLimit) || player.ItemExpands >= ticketLevel)
			return InventoryExpansionPlan.Failed(InventoryExpansionFailure.CubeCannotExpand);

		return new InventoryExpansionPlan(
			InventoryExpansionFailure.None,
			InventoryExpansionStorage.Cube,
			player.ItemExpands + 1,
			player.WarehouseBonusExpands);
	}

	private static bool CanExpandCube(Player player, int cubeExpansionLimit)
	{
		// Java parity: services/CubeExpandService.canExpand.
		var newExpansions = player.NpcExpands + player.QuestExpands + player.ItemExpands + 1;
		return newExpansions >= 0 && newExpansions <= cubeExpansionLimit;
	}

	private static InventoryExpansionPlan CreateWarehousePlan(Player player, int ticketLevel)
	{
		if (!CanExpandWarehouse(player)
			|| player.WarehouseBonusExpands - GetCompletedWarehouseQuestCount(player) >= ticketLevel)
		{
			return InventoryExpansionPlan.Failed(InventoryExpansionFailure.WarehouseCannotExpand);
		}

		return new InventoryExpansionPlan(
			InventoryExpansionFailure.None,
			InventoryExpansionStorage.Warehouse,
			player.ItemExpands,
			player.WarehouseBonusExpands + 1);
	}

	private static bool CanExpandWarehouse(Player player)
	{
		// Java parity: services/WarehouseService.canExpand.
		var newExpansions = player.WarehouseNpcExpands + player.WarehouseBonusExpands + 1;
		return newExpansions >= 0 && newExpansions <= WarehouseExpansionLimit;
	}

	private static int GetCompletedWarehouseQuestCount(Player player)
	{
		// Java parity: WarehouseService.getCompletedWhQuests, quest ids 1987 and 2985.
		return player.Quests.Count(quest => WarehouseQuestIds.Contains(quest.QuestId) && quest.IsComplete);
	}
}

public sealed record InventoryExpansionPlan(
	InventoryExpansionFailure Failure,
	InventoryExpansionStorage Storage,
	int NewItemExpands,
	int NewWarehouseBonusExpands)
{
	public bool Succeeded => Failure == InventoryExpansionFailure.None;

	public static InventoryExpansionPlan Failed(InventoryExpansionFailure failure)
	{
		return new InventoryExpansionPlan(failure, InventoryExpansionStorage.None, 0, 0);
	}
}

public enum InventoryExpansionStorage
{
	None,
	Cube,
	Warehouse,
}

public enum InventoryExpansionFailure
{
	None,
	MissingAction,
	CubeCannotExpand,
	WarehouseCannotExpand,
}
