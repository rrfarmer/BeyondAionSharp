using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/ReturnEffect : EffectTemplate. applyEffect→TeleportService.MoveToBindLocation((Player)effector); calculate→addSuccessEffect if spawned. EffectTemplate/Effect/TeleportService red-tolerated.</summary>
[XmlType("ReturnEffect")]
public class ReturnEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        TeleportService.MoveToBindLocation((Player)effect.GetEffector());
    }

    public override void Calculate(Effect effect)
    {
        if (effect.GetEffected().IsSpawned())
            effect.AddSuccessEffect(this);
    }
}
