using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SkillAttackInstantEffect (ATracer) : DamageEffect.</summary>
[XmlType("SkillAttackInstantEffect")]
public class SkillAttackInstantEffect : DamageEffect
{
    [XmlAttribute]
    public int rnddmg; // TODO should be enum and different types of random damage behaviour
    [XmlAttribute]
    public bool cannotmiss;

    public int GetRnddmg()
    {
        return rnddmg;
    }

    public bool IsCannotmiss()
    {
        return cannotmiss;
    }

    public override bool IsNoResist()
    {
        return cannotmiss || base.IsNoResist();
    }
}
