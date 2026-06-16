namespace Aion.GameServer.Dataholders;

// Shared stat-modifier projection used by ItemSet / Title / ItemRandomBonus dataholder tables.
public sealed record ItemStatModifier(
	string Operation,
	string Name,
	int Value,
	bool Bonus,
	int ChargeCondition = 0)
{
	public int Priority => Operation switch
	{
		"rate" => Bonus ? 50 : 20,
		"set" or "abs" => Bonus ? 70 : 40,
		_ => Bonus ? 60 : 30,
	};
}
