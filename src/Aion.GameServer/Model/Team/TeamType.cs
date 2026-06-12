namespace Aion.GameServer.Model.Team;

/// <summary>
/// Team kind (group/alliance + auto/offence/defence variants) with client type/subType.
/// Java parity: model/team/TeamType.
/// </summary>
public enum TeamType
{
    GROUP,
    AUTO_GROUP,
    ALLIANCE,
    AUTO_ALLIANCE,
    ALLIANCE_DEFENCE,
    ALLIANCE_OFFENCE,
}

public static class TeamTypeExtensions
{
    // Java parity: per-constant (type, subType).
    private static readonly Dictionary<TeamType, (int Type, int SubType)> Data = new()
    {
        [TeamType.GROUP] = (0x3F, 0),
        [TeamType.AUTO_GROUP] = (0x02, 1),
        [TeamType.ALLIANCE] = (0x3F, 0),
        [TeamType.AUTO_ALLIANCE] = (0x36, 1),
        [TeamType.ALLIANCE_DEFENCE] = (0x3F, 4),
        [TeamType.ALLIANCE_OFFENCE] = (0x02, 3),
    };

    // Java parity: getType() — named GetTypeValue (an extension GetType() would be shadowed by Object.GetType()).
    public static int GetTypeValue(this TeamType t) => Data[t].Type;

    // Java parity: getType() - GetType_ is the project-wide getType() convention name; alias to GetTypeValue.
    public static int GetType_(this TeamType t) => Data[t].Type;

    // Java parity: getSubType()
    public static int GetSubType(this TeamType t) => Data[t].SubType;

    // Java parity: isAutoTeam()
    public static bool IsAutoTeam(this TeamType t) => t.GetTypeValue() == 0x02;

    // Java parity: isOffence()
    public static bool IsOffence(this TeamType t) => t.GetSubType() == 3;

    // Java parity: isDefence()
    public static bool IsDefence(this TeamType t) => t.GetSubType() == 4;
}
