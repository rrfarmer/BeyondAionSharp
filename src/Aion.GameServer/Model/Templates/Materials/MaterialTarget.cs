using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Model.Templates.Materials;

/// <summary>Java parity: model/templates/materials/MaterialTarget (Rolandas).</summary>
[XmlType("MaterialTarget")]
public enum MaterialTarget
{
    ALL,
    NPC,
    PLAYER,
    PLAYER_WITH_PET
}

public static class MaterialTargetExtensions
{
    public static bool Matches(this MaterialTarget t, Creature creature) => t switch
    {
        MaterialTarget.ALL => true,
        MaterialTarget.NPC => creature is Npc,
        MaterialTarget.PLAYER => creature is Player,
        MaterialTarget.PLAYER_WITH_PET => MaterialTarget.PLAYER.Matches(creature) || (creature is Summon summon && summon.GetMaster() != null),
        _ => false,
    };
}
