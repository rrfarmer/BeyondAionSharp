using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Tradelist;

/// <summary>Java parity: model/templates/tradelist/TradeNpcType (namedrisk).</summary>
[XmlType("npc_type")]
public enum TradeNpcType
{
    NORMAL,
    ABYSS,
    LEGION_COIN,
    REWARD,
    ABYSS_KINAH // General Shop
}

public static class TradeNpcTypeExtensions
{
    public static int Index(this TradeNpcType t) => t switch
    {
        TradeNpcType.NORMAL => 1,
        TradeNpcType.ABYSS => 2,
        TradeNpcType.LEGION_COIN => 3,
        TradeNpcType.REWARD => 4,
        TradeNpcType.ABYSS_KINAH => 5,
        _ => throw new ArgumentOutOfRangeException(),
    };
}
