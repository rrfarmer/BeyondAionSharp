namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerGroupLootRules(
	PlayerGroupLootRuleType LootRule,
	int Misc,
	int CommonItemAbove,
	int SuperiorItemAbove,
	int HeroicItemAbove,
	int FabledItemAbove,
	int EternalItemAbove,
	int MythicItemAbove)
{
	public static PlayerGroupLootRules Default()
	{
		// Java parity: model/team/common/legacy/LootGroupRules default constructor.
		return new PlayerGroupLootRules(
			PlayerGroupLootRuleType.RoundRobin,
			Misc: 0,
			CommonItemAbove: 0,
			SuperiorItemAbove: 2,
			HeroicItemAbove: 2,
			FabledItemAbove: 2,
			EternalItemAbove: 2,
			MythicItemAbove: 2);
	}

	public int AutoDistributionId
	{
		get
		{
			// Java parity: model/team/common/legacy/LootGroupRules.getAutodistributionId.
			return MythicItemAbove == 3 ? 3 : MythicItemAbove == 2 ? 2 : 0;
		}
	}
}
