using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/TargetChangeEffect (Bobobear) : EffectTemplate. applyEffect: effected is Player player; switch(Delta) case 1→target=effector (case 0 implicit null = Shimmerbomb); player.setTarget(target). VisibleObject red-tolerated.</summary>
[XmlType("TargetChangeEffect")]
public class TargetChangeEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        if (effected is Player player)
        {
            VisibleObject target = null;
            switch (Delta)
            {
                // case 0: Shimmerbomb sets target to null
                case 1:
                    target = effect.GetEffector();
                    break;
            }
            player.SetTarget(target);
        }
    }
}
