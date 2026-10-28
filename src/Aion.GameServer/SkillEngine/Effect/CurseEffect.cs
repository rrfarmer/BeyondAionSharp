using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/CurseEffect (ATracer) : BufEffect. calculate→base.Calculate(effect, CURSE_RESISTANCE, null); start/end set/unset AbnormalState.CURSE. StatEnum/AbnormalState red-tolerated.</summary>
[XmlType("CurseEffect")]
public class CurseEffect : BufEffect
{
    public override void ApplyEffect(Effect effect)
    {
        if(!RetailNpcStatSpellDrain.TryGetCurseProgram(effect,out _))
        {
            base.ApplyEffect(effect);return;
        }
        if(RetailNpcStatSpellDrain.TryScheduleVampireCurseForOfflineFixture(effect,this,
            ()=>ApplyPreparedCurse(effect)))return;
        ApplyPreparedCurse(effect);
    }

    private void ApplyPreparedCurse(Effect effect)
    {
        // A prepared curse may reject a stale resource plan after the effect
        // controller has registered it. Retire that exact effect on failure;
        // do not leave a ghost entry or switch to the host curse halfway through.
        try { base.ApplyEffect(effect); }
        catch { effect.EndEffect(); throw; }
    }

    public override void Calculate(Effect effect)
    {
        if(RetailNpcStatSpellDrain.TryCalculateCurse(effect,this))return;
        base.Calculate(effect, StatEnum.CURSE_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        if(RetailNpcCurseResourceOwner.TryStart(effect,this))return;
        base.StartEffect(effect);
        effect.SetAbnormal(AbnormalState.CURSE);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.CURSE);
    }

    public override void EndEffect(Effect effect)
    {
        if(RetailNpcCurseResourceOwner.TryEnd(effect,this))return;
        base.EndEffect(effect);
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.CURSE);
    }
}
