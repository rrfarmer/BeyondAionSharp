namespace Aion.GameServer.Model.Items;

// Java parity: model/items/PendingTuneResult.
public sealed record PendingTuneResult(
	int OptionalSockets,
	int EnchantBonus,
	int StatBonusId,
	bool IsAttributeOnly)
{
	public int GetOptionalSockets() => OptionalSockets;
	public int GetEnchantBonus() => EnchantBonus;
	public int GetStatBonusId() => StatBonusId;
}
