using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/AbstractAbsoluteStatEffect.</summary>
public abstract class AbstractAbsoluteStatEffect : BufEffect
{
    [XmlAttribute("statsetid")]
    public int statSetId;

    protected override List<IStatFunction> GetModifiers(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        List<IStatFunction> modifiers = new List<IStatFunction>();
        modifiers.AddRange(GetModifiersSet().GetModifiers());

        return modifiers;
    }

    public ModifiersTemplate GetModifiersSet()
    {
        return DataManager.ABSOLUTE_STATS_DATA.GetTemplate(statSetId);
    }
}
