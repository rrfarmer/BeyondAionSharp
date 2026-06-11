using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
 using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/DPHealInstantEffect : AbstractHealEffect. DP instant heal; calculate/applyEffect delegate to base with HealType.DP. Effect/lifeStats red-tolerated.</summary>
[XmlType("DPHealInstantEffect")]
public class DPHealInstantEffect : AbstractHealEffect
{
    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, HealType.DP);
    }

    public override void ApplyEffect(Effect effect)
    {
        base.ApplyEffect(effect, HealType.DP);
    }

    public override int GetCurrentStatValue(Effect effect)
    {
        return ((Player)effect.GetEffected()).GetCommonData().GetDp();
    }

    public override int GetMaxStatValue(Effect effect)
    {
        return ((Player)effect.GetEffected()).GetGameStats().GetMaxDp().GetCurrent();
    }
}
