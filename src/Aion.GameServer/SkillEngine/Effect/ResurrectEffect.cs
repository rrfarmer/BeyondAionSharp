using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/ResurrectEffect (ATracer) : EffectTemplate. @XmlAttribute(name="skill_id")→[XmlAttribute("skill_id")]; applyEffect: Player→setPlayerResActivate(true), setResurrectionSkill(skillId), SM_RESURRECT packet; calculate: Player && isDead→base.Calculate(effect,null,null). SM_RESURRECT/Player red-tolerated.</summary>
[XmlType("ResurrectEffect")]
public class ResurrectEffect : EffectTemplate
{
    [XmlAttribute("skill_id")]
    protected int skillId;

    public override void ApplyEffect(Effect effect)
    {
        if (effect.GetEffected() is Player)
        {
            Player effectedPlayer = (Player)effect.GetEffected();
            effectedPlayer.SetPlayerResActivate(true);
            effectedPlayer.SetResurrectionSkill(skillId);
            PacketSendUtility.SendPacket(effectedPlayer, new SM_RESURRECT(effect.GetEffector(), effect.GetSkillId()));
        }
    }

    public override void Calculate(Effect effect)
    {
        if (effect.GetEffected() is Player && effect.GetEffected().IsDead())
            base.Calculate(effect, null, null);
    }
}
