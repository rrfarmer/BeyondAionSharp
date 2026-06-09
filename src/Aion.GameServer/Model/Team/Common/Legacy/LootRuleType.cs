namespace Aion.GameServer.Model.Team.Common.Legacy;

/// <summary>Java parity: model/team/common/legacy/LootRuleType (Lyahim). Id-bearing enum (backing value == Java id).</summary>
public enum LootRuleType
{
    FREEFORALL = 0,
    ROUNDROBIN = 1,
    LEADER = 2,
}

public static class LootRuleTypeExtensions
{
    /// <summary>Java parity: getId() — backing value equals the Java per-constant id.</summary>
    public static int GetId(this LootRuleType type) => (int)type;
}
