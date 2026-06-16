using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SkillCooltimeResetEffect (Rolandas, Luzien) : EffectTemplate. @XmlAttribute(name="first_cd"/"last_cd"); applyEffect: HashMap→Dictionary; for i in firstCd..lastCd: delay=getSkillCoolDown(i)-currentTimeMillis [UtcNow.ToUnixTimeMilliseconds]; skip if &lt;=0; Delta>0→delay-=delay*(Delta/100) else delay-=Value; setSkillCoolDown + collect; non-empty && Player→SM_SKILL_COOLDOWN. red-tolerated.</summary>
[XmlType("SkillCooltimeResetEffect")]
public class SkillCooltimeResetEffect : EffectTemplate
{
    [XmlAttribute("first_cd")]
    public int firstCd;

    [XmlAttribute("last_cd")]
    public int lastCd;

    public override void ApplyEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        Dictionary<int, long> resetSkillCoolDowns = new Dictionary<int, long>();
        for (int i = firstCd; i <= lastCd; i++)
        {
            long delay = effected.GetSkillCoolDown(i) - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (delay <= 0)
                continue;
            if (Delta > 0) // TODO: Percent of remaining CD or original cd?
                delay -= delay * (Delta / 100);
            else
                delay -= Value;

            effected.SetSkillCoolDown(i, delay + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            resetSkillCoolDowns[i] = delay + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        if (resetSkillCoolDowns.Count != 0 && effected is Player player)
        {
            PacketSendUtility.SendPacket(player, new SM_SKILL_COOLDOWN(player, resetSkillCoolDowns, true));
        }
    }
}
