using System.Xml.Serialization;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/MpAttackInstantEffect (Sippolo) : EffectTemplate. @XmlAttribute percent; calculate: maxMP, percent→(maxMP*Value)/100, setReserveds(new EffectReserved(Position, newValue, MP, true), false), this.Calculate(effect,null,null,Element) [4-arg]; applyEffect: reduceMp(DAMAGE_MP, reserveds(Position), skillId, MPATTACK). EffectReserved/ResourceType red-tolerated.</summary>
[XmlType("MpAttackInstantEffect")]
public class MpAttackInstantEffect : EffectTemplate
{
    [XmlAttribute]
    protected bool percent;

    public override void Calculate(Effect effect)
    {
        int maxMP = effect.GetEffected().GetLifeStats().GetMaxMp();
        int newValue = Value;
        // Support for values in percentage
        if (percent)
            newValue = ((maxMP * Value) / 100);

        effect.SetReserveds(new EffectReserved(Position, newValue, EffectReserved.ResourceType.MP, true), false);

        this.Calculate(effect, null, null, Element);
    }

    public override void ApplyEffect(Effect effect)
    {
        effect.GetEffected().GetLifeStats().ReduceMp(SM_ATTACK_STATUS.TYPE.DAMAGE_MP, effect.GetReserveds(Position).GetValue(), effect.GetSkillId(), SM_ATTACK_STATUS.LOG.MPATTACK);
    }
}
