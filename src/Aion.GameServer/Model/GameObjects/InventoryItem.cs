namespace Aion.GameServer.Model.GameObjects;

public sealed class InventoryItem
{
	public int ObjectId { get; init; }

	public int ItemId { get; init; }

	public long Count { get; init; }

	public int? Color { get; init; }

	public int ColorExpires { get; init; }

	public string? Creator { get; init; }

	public int ExpireTime { get; init; }

	public int ActivationCount { get; init; }

	public int OwnerId { get; init; }

	public bool IsEquipped { get; init; }

	public bool IsSoulBound { get; init; }

	public long Slot { get; init; }

	public int Location { get; init; }

	public int Enchant { get; init; }

	public int EnchantBonus { get; init; }

	public int ItemSkin { get; init; }

	public int FusionedItem { get; init; }

	public int OptionalSocket { get; init; }

	public int OptionalFusionSocket { get; init; }

	public int Charge { get; init; }

	public int TuneCount { get; init; }

	public int RandomBonus { get; init; }

	public int FusionRandomBonus { get; init; }

	public int Tempering { get; init; }

	public int PackCount { get; init; }

	public bool IsAmplified { get; init; }

	public int BuffSkill { get; init; }

	public int RandomPlumeBonus { get; init; }
}
