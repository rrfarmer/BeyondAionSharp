using System;
using Aion.GameServer.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/CarveSignetEffect (ATracer) : DamageEffect. @XmlAttribute signet_increment/signet_cap/signet_id/signet/prob (required dropped); applyEffect: Rnd.chance>=prob→return; getAbnormalEffect(signet); active→endEffect + nextSignetLevel=min(carved+incr, max(cap, carved)); applyEffect(signetId+next-1)→setCarvedSignet. Local Effect `signet`→`signetEffect` (C# forbids local shadowing field of same name). SkillEngine red-tolerated.</summary>
[XmlType("CarveSignetEffect")]
public class CarveSignetEffect : DamageEffect
{
    [XmlAttribute("signet_increment")]
    public int signetIncrement = 1;
    [XmlAttribute("signet_cap")]
    public int signetCap;
    [XmlAttribute("signet_id")]
    public int signetId;
    [XmlAttribute]
    public string signet;
    [XmlAttribute]
    public int prob = 100;

    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect);

        if (Rnd.Chance() >= prob)
            return;

        int nextSignetLevel = signetIncrement;
        Effect activeSignet = effect.GetEffected().GetEffectController().GetAbnormalEffect(signet);
        if (activeSignet != null)
        {
            activeSignet.EndEffect();
            nextSignetLevel = Math.Min(activeSignet.GetCarvedSignet() + signetIncrement, Math.Max(signetCap, activeSignet.GetCarvedSignet()));
        }
        Effect signetEffect = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffect(signetId + nextSignetLevel - 1, effect.GetEffector(), effect.GetEffected());
        signetEffect.SetCarvedSignet(nextSignetLevel);
    }
}
