namespace Aion.GameServer.Model;

/// <summary>
/// Client-facing creature type/disposition (attackable, peace, aggressive, friend, …).
/// Java parity: model/CreatureType.
/// </summary>
public enum CreatureType
{
    /// <summary>These are regular monsters</summary>
    ATTACKABLE = 0,
    /// <summary>These are Peace npc, which you cannot talk to</summary>
    PEACE = 2,
    /// <summary>These are monsters that are pre-aggressive</summary>
    AGGRESSIVE = 8,
    // unk
    INVULNERABLE = 10,
    /// <summary>These are non attackable NPCs, which you can talk to</summary>
    FRIEND = 38,

    SUPPORT = 54,
}

public static class CreatureTypeExtensions
{
    // Java parity: getId()
    public static int GetId(this CreatureType type) => (int)type;
}
