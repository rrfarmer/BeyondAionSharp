using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/SanctuaryEffect : EffectTemplate. equals→Equals; setAbnormal/unsetAbnormal SANCTUARY; AbnormalState same-namespace. EffectTemplate/Effect red-tolerated.</summary>
public class SanctuaryEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
        if (effect.GetEffector().Equals(effect.GetEffected()))
            effect.GetEffected().SetTarget(effect.GetEffected());
    }

    public override void StartEffect(Effect effect)
    {
        effect.SetAbnormal(AbnormalState.SANCTUARY);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.SANCTUARY);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.SANCTUARY);
    }
}
