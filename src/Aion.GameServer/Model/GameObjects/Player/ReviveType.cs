using System;

namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>Java parity: model/gameobjects/player/ReviveType. Java enum w/ per-instance typeId (non-sequential: 0,1,2,3,4,6,8) → enum + extensions.</summary>
public enum ReviveType
{
    /// <summary>Revive to bindpoint</summary>
    BIND_REVIVE,
    /// <summary>Revive from rebirth effect</summary>
    REBIRTH_REVIVE,
    /// <summary>Self-Rez Stone</summary>
    ITEM_SELF_REVIVE,
    /// <summary>Revive from skill</summary>
    SKILL_REVIVE,
    /// <summary>Revive to Kisk</summary>
    KISK_REVIVE,
    /// <summary>Revive to Instance Start point</summary>
    INSTANCE_REVIVE,
    /// <summary>Revive to Obelisk</summary>
    OBELISK_REVIVE
}

public static class ReviveTypeExtensions
{
    public static int GetReviveTypeId(this ReviveType t) => t switch
    {
        ReviveType.BIND_REVIVE => 0,
        ReviveType.REBIRTH_REVIVE => 1,
        ReviveType.ITEM_SELF_REVIVE => 2,
        ReviveType.SKILL_REVIVE => 3,
        ReviveType.KISK_REVIVE => 4,
        ReviveType.INSTANCE_REVIVE => 6,
        ReviveType.OBELISK_REVIVE => 8,
        _ => 0,
    };

    public static ReviveType GetReviveTypeById(int id)
    {
        foreach (ReviveType rt in Enum.GetValues<ReviveType>())
        {
            if (rt.GetReviveTypeId() == id)
                return rt;
        }
        throw new ArgumentException("Unsupported revive type: " + id);
    }
}
