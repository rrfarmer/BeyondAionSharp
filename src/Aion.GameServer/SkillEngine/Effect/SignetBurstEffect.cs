using System;
using Aion.GameServer.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SignetBurstEffect (ATracer, kecimis) : DamageEffect. @XmlAttribute signetlvl/signet; @XmlAttribute(name="add_effect_prob_multi"); calculateDamage: base value, Element!=NONE→*knowledge/100f (lossy int*=float preserved); SIGNET_DATA_TEMPLATES.getSignetData(SignetEnum.valueOf(signet)→Enum.Parse, lvl); *damageMultiplier, effectProb*=multi; AttackUtil.calculateSkillResult; setLaunchSubEffect(Rnd.chance<effectProb); endEffect. calculate: base.Calculate(effect,null,null) false→endEffect. SignetData/SignetEnum red-tolerated.</summary>
[XmlType("SignetBurstEffect")]
public class SignetBurstEffect : DamageEffect
{
    [XmlAttribute]
    protected int signetlvl;
    [XmlAttribute]
    protected string signet;
    [XmlAttribute("add_effect_prob_multi")]
    protected int addEffectProbMultiplier = 0;

    public override void CalculateDamage(Effect effect)
    {
        Effect signetEffect = effect.GetEffected().GetEffectController().GetAbnormalEffect(signet);
        int valueWithDelta = CalculateBaseValue(effect);
        if (Element != SkillElement.NONE)
            valueWithDelta = (int)(valueWithDelta * (effect.GetEffector().GetGameStats().GetKnowledge().GetCurrent() / 100f));

        int effectProb = 0;
        SignetData signetData = DataManager.SIGNET_DATA_TEMPLATES.GetSignetData(Enum.Parse<SignetEnum>(signet), signetEffect == null ? 0 : signetEffect.GetSkillLevel());
        if (signetData != null)
        {
            valueWithDelta = (int)(valueWithDelta * signetData.GetDamageMultiplier());
            effectProb = signetData.GetAddEffectProb() * addEffectProbMultiplier;
        }
        AttackUtil.CalculateSkillResult(effect, valueWithDelta, this, false);
        effect.SetLaunchSubEffect(Rnd.Chance() < effectProb);
        if (signetEffect != null)
            signetEffect.EndEffect();
    }

    public override void Calculate(Effect effect)
    {
        Effect signetEffect = effect.GetEffected().GetEffectController().GetAbnormalEffect(signet);
        if (!base.Calculate(effect, null, null))
        {
            if (signetEffect != null)
            {
                signetEffect.EndEffect();
            }
        }
    }

    public int GetSignetlvl()
    {
        return signetlvl;
    }

    public string GetSignet()
    {
        return signet;
    }
}
