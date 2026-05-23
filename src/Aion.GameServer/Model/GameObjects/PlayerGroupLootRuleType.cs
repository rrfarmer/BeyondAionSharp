namespace Aion.GameServer.Model.GameObjects;

public enum PlayerGroupLootRuleType
{
	// Java parity: model/team/common/legacy/LootRuleType.FREEFORALL.
	FreeForAll = 0,

	// Java parity: model/team/common/legacy/LootRuleType.ROUNDROBIN.
	RoundRobin = 1,

	// Java parity: model/team/common/legacy/LootRuleType.LEADER.
	Leader = 2,
}
