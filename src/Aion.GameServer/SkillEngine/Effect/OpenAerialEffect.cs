using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/OpenAerialEffect (ATracer) : EffectTemplate. applyEffect→addToEffectedController; calculate: 5-state guard, OPENAERIAL_RESISTANCE+SpellStatus.OPENAERIAL, subEffect non-Player→SubEffectType.OPENAERIAL; if !flying→geoZ via GeoService.getZ (Float.isNaN→float.IsNaN guard), setTargetLoc(x,y,z); startEffect: cancelCurrentSkill, removeParalyzeEffects, Player glide/move stop, World.updatePosition, Player SM_FORCED_MOVE, set OPENAERIAL; endEffect→unset. red-tolerated.</summary>
[XmlType("OpenAerialEffect")]
public class OpenAerialEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        if (effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.PULLED)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.STUMBLE)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.OPENAERIAL)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.STAGGER)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.SPIN))
            return;

        if (!base.Calculate(effect, StatEnum.OPENAERIAL_RESISTANCE, SpellStatus.OPENAERIAL))
            return;
        if (effect.IsSubEffect() && !(effect.GetEffected() is Player))
            effect.SetSubEffectType(SubEffectType.OPENAERIAL);
        float z = effect.GetEffected().GetZ();
        if (!effect.GetEffected().IsFlying())
        {
            float geoZ = GeoService.GetInstance().GetZ(effect.GetEffected().GetWorldId(), effect.GetEffected().GetX(), effect.GetEffected().GetY(), effect.GetEffected().GetZ() + 2, effect.GetEffected().GetZ() - 1, effect.GetEffected().GetInstanceId());
            if (!float.IsNaN(geoZ))
            {
                z = geoZ;
            }
        }
        effect.SetTargetLoc(effect.GetEffected().GetX(), effect.GetEffected().GetY(), z);
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetController().CancelCurrentSkill(effect.GetEffector());
        effect.GetEffected().GetEffectController().RemoveParalyzeEffects();
        if (effected is Player player)
        {
            player.GetFlyController().OnStopGliding();
            player.GetController().OnStopMove();
        }
        World.GetInstance().UpdatePosition(effected, effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ(), effected.GetHeading());
        if (effected is Player)
            PacketSendUtility.BroadcastPacketAndReceive(effected, new SM_FORCED_MOVE(effect.GetEffector(), effected.GetObjectId(),
                    effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ()));
        effect.SetAbnormal(AbnormalState.OPENAERIAL);
        effected.GetEffectController().SetAbnormal(AbnormalState.OPENAERIAL);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.OPENAERIAL);
    }
}
