using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/InvulnerableWingEffect (VladimirZ, Sippolo) : EffectTemplate. calculate: Player-only→base.Calculate(effect,null,null); applyEffect→addToEffectedController + setAbnormal(INVULNERABLE_WING); endEffect→unsetAbnormal(INVULNERABLE_WING). AbnormalState red-tolerated.</summary>
[XmlType("InvulnerableWingEffect")]
public class InvulnerableWingEffect : EffectTemplate
{
    public override void Calculate(Effect effect)
    {
        // Only for players
        if (effect.GetEffected() is Player)
            base.Calculate(effect, null, null);
    }

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.INVULNERABLE_WING);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.INVULNERABLE_WING);
    }
}
