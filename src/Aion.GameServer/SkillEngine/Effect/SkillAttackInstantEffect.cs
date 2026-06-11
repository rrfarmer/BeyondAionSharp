using System.Xml.Serialization;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/SkillAttackInstantEffect (ATracer) : DamageEffect. @XmlAttribute rnddmg/cannotmiss; getRnddmg/isCannotmiss getters; canDodgeOrResist override: cannotmiss→false else base.CanDodgeOrResist. base virtual via convergence edit. Effect red-tolerated.</summary>
[XmlType("SkillAttackInstantEffect")]
public class SkillAttackInstantEffect : DamageEffect
{
    [XmlAttribute]
    protected int rnddmg; // TODO should be enum and different types of random damage behaviour
    [XmlAttribute]
    protected bool cannotmiss;

    public int GetRnddmg()
    {
        return rnddmg;
    }

    public bool IsCannotmiss()
    {
        return cannotmiss;
    }

    protected override bool CanDodgeOrResist(Effect effect)
    {
        if (cannotmiss)
            return false;
        return base.CanDodgeOrResist(effect);
    }
}
