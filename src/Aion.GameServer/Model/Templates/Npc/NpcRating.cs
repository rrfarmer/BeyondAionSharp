using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.State;

namespace Aion.GameServer.Model.Templates.Npc;

/// <summary>
/// NPC quality rating — controls default see-state.
/// Java parity: model/templates/npc/NpcRating.
/// </summary>
[XmlType("rating")]
public enum NpcRating
{
    Junk,
    Normal,
    Elite,
    Hero,
    Legendary,
}

public static class NpcRatingExtensions
{
    // Java parity: NpcRating::getCongenitalSeeState()
    public static CreatureSeeState GetCongenitalSeeState(this NpcRating rating) => rating switch
    {
        NpcRating.Junk => CreatureSeeState.Normal,
        NpcRating.Normal => CreatureSeeState.Normal,
        NpcRating.Elite => CreatureSeeState.Search1,
        NpcRating.Hero => CreatureSeeState.Search2,
        NpcRating.Legendary => CreatureSeeState.Search2,
        _ => CreatureSeeState.Normal,
    };
}
