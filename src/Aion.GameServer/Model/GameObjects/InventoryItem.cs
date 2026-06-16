using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/gameobjects/Persistable.PersistentState.
public enum InventoryItemPersistentState
{
	New,
	UpdateRequired,
	Updated,
	Deleted,
	NoAction,
}

public sealed class InventoryItem
{
	// Java parity: model/gameobjects/Item.setPersistentState(PersistentState).
	public static InventoryItemPersistentState TransitionPersistentState(
		InventoryItemPersistentState currentState,
		InventoryItemPersistentState requestedState)
	{
		return requestedState switch
		{
			InventoryItemPersistentState.Deleted => currentState == InventoryItemPersistentState.New
				? InventoryItemPersistentState.NoAction
				: InventoryItemPersistentState.Deleted,
			InventoryItemPersistentState.UpdateRequired => currentState == InventoryItemPersistentState.New
				? InventoryItemPersistentState.New
				: InventoryItemPersistentState.UpdateRequired,
			_ => requestedState,
		};
	}

	public int ObjectId { get; init; }

	public int ItemId { get; init; }

	// Java parity: model/gameobjects/Item.decreaseItemCount / increaseItemCount mutate count in place.
	public long Count { get; set; }

	public int? Color { get; init; }

	public int ColorExpires { get; init; }

	public string? Creator { get; init; }

	public int ExpireTime { get; init; }

	public int ActivationCount { get; init; }

	// Java parity: InventoryDAO.getItemOwnerId rewrites row owner when an item crosses account/legion/player storage.
	public int OwnerId { get; set; }

	public bool IsEquipped { get; init; }

	public bool IsSoulBound { get; init; }

	// Java parity: model/gameobjects/Item.setEquipmentSlot mutates slot in place.
	public long Slot { get; set; }

	// Java parity: item.setItemLocation mutates storage type (location) in place after cross-storage move.
	public int Location { get; set; }

	public int Enchant { get; init; }

	public int EnchantBonus { get; init; }

	public int ItemSkin { get; init; }

	public int FusionedItem { get; init; }

	public int OptionalSocket { get; init; }

	public int OptionalFusionSocket { get; init; }

	public int Charge { get; init; }

	public int TuneCount { get; init; }

	// Java parity: model/gameobjects/Item.isIdentified.
	public bool IsIdentified => TuneCount != -1;

	public int RandomBonus { get; init; }

	public int FusionRandomBonus { get; init; }

	public int Tempering { get; init; }

	// Java parity: model/gameobjects/Item.setPackCount mutates pack count in place (positive=wrapped, negative=unwrapped).
	public int PackCount { get; set; }

	public bool IsAmplified { get; init; }

	public int BuffSkill { get; init; }

	public int RandomPlumeBonus { get; init; }

	// Java parity: model/gameobjects/Item.pendingTuneResult.
	public PendingTuneResult? PendingTuneResult { get; set; }

	// Java parity: model/gameobjects/Item.persistentState.
	public InventoryItemPersistentState PersistentState { get; init; } = InventoryItemPersistentState.Updated;

	public IReadOnlyList<ItemStoneSocket> ManaStones { get; set; } = Array.Empty<ItemStoneSocket>();

	public IReadOnlyList<ItemStoneSocket> FusionStones { get; set; } = Array.Empty<ItemStoneSocket>();

	public PlayerGodstone? Godstone { get; set; }

	public PlayerIdianStone? IdianStone { get; set; }
}

// Java parity: model/items/ManaStone for item_stones category MANASTONE and FUSIONSTONE.
public sealed record ItemStoneSocket(int ItemId, int Slot);

// Java parity: model/items/GodStone restored by dao/ItemStoneListDAO.load.
public sealed record PlayerGodstone(int ItemId, int ProcCount);

// Java parity: model/items/IdianStone restored by dao/ItemStoneListDAO.load.
public sealed record PlayerIdianStone(int ItemId, int PolishNumber, int PolishCharge);
