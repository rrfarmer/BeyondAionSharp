using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
 using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/ProcDPHealInstantEffect : AbstractHealEffect. DP instant heal; calculate/applyEffect delegate to base with HealType.DP. Effect/lifeStats red-tolerated.</summary>
[XmlType("ProcDPHealInstantEffect")]
public class ProcDPHealInstantEffect : AbstractHealEffect
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
