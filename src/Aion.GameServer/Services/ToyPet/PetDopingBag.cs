namespace Aion.GameServer.Model.Templates.Pet;

public sealed class PetDopingBag
{
	public const int MaxItems = 8;

	private readonly object _sync = new();
	private int[]? _itemBag;

	public bool IsDirty { get; private set; }

	public int FoodItem => _itemBag is { Length: >= 1 } ? _itemBag[0] : 0;

	public int DrinkItem => _itemBag is { Length: >= 2 } ? _itemBag[1] : 0;

	public void SetFoodItem(int itemId)
	{
		SetItem(itemId, slot: 0);
	}

	public void SetDrinkItem(int itemId)
	{
		SetItem(itemId, slot: 1);
	}

	public void SetItem(int itemId, int slot)
	{
		// Java parity: model/templates/pet/PetDopingBag.setItem is synchronized and grows the backing array to the touched slot.
		lock (_sync)
		{
			if (slot < 0 || slot >= MaxItems)
			{
				throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Slot index {slot} for item {itemId} is invalid.");
			}

			if (_itemBag == null || slot >= _itemBag.Length)
			{
				Array.Resize(ref _itemBag, slot + 1);
			}

			if (_itemBag[slot] != itemId)
			{
				_itemBag[slot] = itemId;
				IsDirty = true;
			}
		}
	}

	public int[] GetScrollsUsed()
	{
		if (_itemBag == null || _itemBag.Length < 3)
		{
			return [];
		}

		return _itemBag[2..];
	}

	public int[] GetItems()
	{
		return _itemBag is null ? [] : (int[])_itemBag.Clone();
	}

	public void SwitchItems(int slot1, int slot2)
	{
		// Java parity: only scroll slots can be relocated; Java dereferences itemBag after this guard.
		if (slot1 < 2 || slot2 < 2)
		{
			return;
		}

		lock (_sync)
		{
			var itemBag = _itemBag ?? throw new NullReferenceException("PetDopingBag item storage is not initialized.");
			var slot1Item = itemBag.Length > slot1 ? itemBag[slot1] : 0;
			var slot2Item = itemBag.Length > slot2 ? itemBag[slot2] : 0;
			SetItem(slot1Item, slot2);
			SetItem(slot2Item, slot1);
		}
	}
}
