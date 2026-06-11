using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/ResurrectPositionalEffect (Sippolo) : ResurrectEffect. applyEffect: (Player) effector/effected casts; setPlayerResActivate(true), setResurrectionSkill(skillId), SM_RESURRECT, setResPosState(true), setResPos{X,Y,Z}(effector pos); calculate: effector Player && effected Player && isDead→base.Calculate(effect,null,null). SM_RESURRECT/Player red-tolerated.</summary>
[XmlType("ResurrectPositionalEffect")]
public class ResurrectPositionalEffect : ResurrectEffect
{
    public override void ApplyEffect(Effect effect)
    {
        Player effector = (Player)effect.GetEffector();
        Player effected = (Player)effect.GetEffected();

        effected.SetPlayerResActivate(true);
        effected.SetResurrectionSkill(skillId);
        PacketSendUtility.SendPacket(effected, new SM_RESURRECT(effect.GetEffector(), effect.GetSkillId()));
        effected.SetResPosState(true);
        effected.SetResPosX(effector.GetX());
        effected.SetResPosY(effector.GetY());
        effected.SetResPosZ(effector.GetZ());
    }

    public override void Calculate(Effect effect)
    {
        if ((effect.GetEffector() is Player) && (effect.GetEffected() is Player) && (effect.GetEffected().IsDead()))
            base.Calculate(effect, null, null);
    }
}
