using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.State;

namespace Aion.GameServer.Model.Templates.Npc;

/// <summary>
/// NPC quality rating — controls default see-state.
/// Java parity: model/templates/npc/NpcRating (ATracer). UPPER_CASE names match Java + the XML rating="JUNK" values.
/// </summary>
[XmlType("rating")]
public enum NpcRating
{
    JUNK,
    NORMAL,
    ELITE,
    HERO,
    LEGENDARY,
}

public static class NpcRatingExtensions
{
    // Java parity: NpcRating::getCongenitalSeeState()
    public static CreatureSeeState GetCongenitalSeeState(this NpcRating rating) => rating switch
    {
        NpcRating.JUNK => CreatureSeeState.Normal,
        NpcRating.NORMAL => CreatureSeeState.Normal,
        NpcRating.ELITE => CreatureSeeState.Search1,
        NpcRating.HERO => CreatureSeeState.Search2,
        NpcRating.LEGENDARY => CreatureSeeState.Search2,
        _ => CreatureSeeState.Normal,
    };
}
