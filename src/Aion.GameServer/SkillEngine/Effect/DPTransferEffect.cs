using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/DPTransferEffect (Sippolo) : EffectTemplate. applyEffect: newValue=reserveds(Position); effected.addDp(+v), effector.addDp(-v); calculate: base.Calculate false→return else setReserveds(new EffectReserved(Position, getCurrentStatValue, DP, true), false); getCurrentStatValue→effector.commonData.getDp(). EffectReserved/ResourceType red-tolerated.</summary>
[XmlType("DPTransferEffect")]
public class DPTransferEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        int newValue = effect.GetReserveds(Position).GetValue();
        ((Player)effect.GetEffected()).GetCommonData().AddDp(newValue);
        ((Player)effect.GetEffector()).GetCommonData().AddDp(-newValue);
    }

    public override void Calculate(Effect effect)
    {
        if (!base.Calculate(effect, null, null))
            return;
        effect.SetReserveds(new EffectReserved(Position, GetCurrentStatValue(effect), EffectReserved.ResourceType.DP, true), false);
    }

    private int GetCurrentStatValue(Effect effect)
    {
        return ((Player)effect.GetEffector()).GetCommonData().GetDp();
    }
}
