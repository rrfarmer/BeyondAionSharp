using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
 using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/DPHealEffect : HealOverTimeEffect. DP heal-over-time; startEffect/onPeriodicAction delegate to base with HealType.DP. Effect/lifeStats red-tolerated.</summary>
[XmlType("DPHealEffect")]
public class DPHealEffect : HealOverTimeEffect
{
    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect, HealType.DP);
    }

    public override void OnPeriodicAction(Effect effect)
    {
        base.OnPeriodicAction(effect, HealType.DP);
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
