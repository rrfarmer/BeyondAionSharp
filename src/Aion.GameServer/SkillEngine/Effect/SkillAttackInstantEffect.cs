using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SkillAttackInstantEffect (ATracer) : DamageEffect. @XmlAttribute rnddmg/cannotmiss; getRnddmg/isCannotmiss getters; canDodgeOrResist override: cannotmiss→false else base.CanDodgeOrResist. base virtual via convergence edit. Effect red-tolerated.</summary>
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

    protected override bool CanDodgeOrResist(Effect effect)
    {
        if (cannotmiss)
            return false;
        return base.CanDodgeOrResist(effect);
    }
}
