using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/ProcVPHealInstantEffect (kecimis, source.com) : EffectTemplate (no @XmlType→class name). @XmlAttribute value2(cap)/percent; applyEffect: Player; cap=maxReposeEnergy*value2/100; if readyForReposeEnergy && current&lt;cap: base value, percent→maxReposeEnergy*v*0.001 else v; addReposeEnergy + SM_STATUPDATE_EXP. PlayerCommonData red-tolerated.</summary>
[XmlType("ProcVPHealInstantEffect")]
public class ProcVPHealInstantEffect : EffectTemplate
{
    [XmlAttribute]
    protected int value2; // cap
    [XmlAttribute]
    protected bool percent;

    public override void ApplyEffect(Effect effect)
    {
        if (effect.GetEffected() is Player)
        {
            Player player = (Player)effect.GetEffected();
            PlayerCommonData pcd = player.GetCommonData();

            long cap = pcd.GetMaxReposeEnergy() * value2 / 100;

            if (pcd.IsReadyForReposeEnergy() && pcd.GetCurrentReposeEnergy() < cap)
            {
                int valueWithDelta = CalculateBaseValue(effect);
                long addEnergy = 0;
                if (percent)
                    addEnergy = (int)(pcd.GetMaxReposeEnergy() * valueWithDelta * 0.001); // recheck when more skills
                else
                    addEnergy = valueWithDelta;

                pcd.AddReposeEnergy(addEnergy);
                PacketSendUtility.SendPacket(
                    player,
                    new SM_STATUPDATE_EXP(pcd.GetExpShown(), pcd.GetExpRecoverable(), pcd.GetExpNeed(), pcd.GetCurrentReposeEnergy(), pcd
                        .GetMaxReposeEnergy()));
            }
        }
    }
}
