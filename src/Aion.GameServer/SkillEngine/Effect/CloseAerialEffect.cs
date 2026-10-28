using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/CloseAerialEffect : EffectTemplate. applyEffect→removeEffect(8224); super.calculate(effect,null,SpellStatus.CLOSEAERIAL)→base.Calculate. EffectTemplate/Effect/SpellStatus red-tolerated.</summary>
[XmlType("CloseAerialEffect")]
public class CloseAerialEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        if(Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatAerialCombat.HasSelectedClose(effect))return;
        effect.GetEffected().GetEffectController().RemoveEffect(8224);
    }

    public override void Calculate(Effect effect)
    {
        if(Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatAerialCombat.TryCalculateClose(effect,this))return;
        base.Calculate(effect, null, SpellStatus.CLOSEAERIAL);
    }
}
