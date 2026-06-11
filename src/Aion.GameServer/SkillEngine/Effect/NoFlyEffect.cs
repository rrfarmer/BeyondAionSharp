using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/NoFlyEffect (Sippolo) : EffectTemplate. calculate: Player-only→base.Calculate(effect, NOFLY_RESISTANCE, null); applyEffect→addToEffectedController; startEffect: endFly(true), set AbnormalState.NOFLY; endEffect→unset. StatEnum/AbnormalState red-tolerated.</summary>
[XmlType("NoFlyEffect")]
public class NoFlyEffect : EffectTemplate
{
    public override void Calculate(Effect effect)
    {
        // Affects only players (for now as we dont have flying Npc's)
        if (effect.GetEffected() is Player)
            base.Calculate(effect, StatEnum.NOFLY_RESISTANCE, null);
    }

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        ((Player)effect.GetEffected()).GetFlyController().EndFly(true);

        effect.SetAbnormal(AbnormalState.NOFLY);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.NOFLY);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.NOFLY);
    }
}
