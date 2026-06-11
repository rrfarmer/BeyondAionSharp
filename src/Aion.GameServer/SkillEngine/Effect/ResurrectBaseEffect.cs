using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/ResurrectBaseEffect : ResurrectEffect. calculate→Calculate(effect,null,null) (inherited 3-arg); applyEffect→addToEffectedController; endEffect: effected dead && is Player player && !isDueling(effector)→PlayerReviveService.scheduleReviveAtBase(player, 2500, skillId). PlayerReviveService red-tolerated.</summary>
[XmlType("ResurrectBaseEffect")]
public class ResurrectBaseEffect : ResurrectEffect
{
    public override void Calculate(Effect effect)
    {
        Calculate(effect, null, null);
    }

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void EndEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        if (effected.IsDead() && effected is Player player && !player.IsDueling(effect.GetEffector()))
            PlayerReviveService.ScheduleReviveAtBase(player, 2500, skillId);
    }
}
