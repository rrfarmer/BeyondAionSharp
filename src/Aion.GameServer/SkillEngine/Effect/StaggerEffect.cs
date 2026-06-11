using System;
using System.Xml.Serialization;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/StaggerEffect (ATracer) : EffectTemplate. applyEffect→addToEffectedController; startEffect: cancelCurrentSkill, removeParalyzeEffects, Player glide/move stop, World.updatePosition to targetLoc, Player→SM_FORCED_MOVE, set STAGGER; calculate: 4-state guard, STAGGER_RESISTANCE+SpellStatus.STAGGER, subEffect non-Player→SubEffectType.STAGGER, 2m backward via getClosestCollision, setTargetLoc; endEffect→unset. Math.toRadians→*PI/180. red-tolerated.</summary>
[XmlType("StaggerEffect")]
public class StaggerEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetController().CancelCurrentSkill(effect.GetEffector());
        effected.GetEffectController().RemoveParalyzeEffects();
        if (effected is Player player)
        {
            player.GetFlyController().OnStopGliding();
            player.GetController().OnStopMove();
        }
        World.GetInstance().UpdatePosition(effected, effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ(), effected.GetHeading());
        if (effected is Player)
            PacketSendUtility.BroadcastPacketAndReceive(effected, new SM_FORCED_MOVE(effect.GetEffector(), effected.GetObjectId(),
                    effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ()));
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.STAGGER);
        effect.SetAbnormal(AbnormalState.STAGGER);
    }

    public override void Calculate(Effect effect)
    {
        if (effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.PULLED)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.STAGGER)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.OPENAERIAL)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.STUMBLE))
            return;

        if (!base.Calculate(effect, StatEnum.STAGGER_RESISTANCE, SpellStatus.STAGGER))
            return;
        if (effect.IsSubEffect() && !(effect.GetEffected() is Player))
            effect.SetSubEffectType(SubEffectType.STAGGER);
        Creature effector = effect.GetEffector();
        Creature effected = effect.GetEffected();
        // Move effected 2 meters backward as on retail
        double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(PositionUtil.GetHeadingTowards(effector, effect.GetEffected()));
        float x1 = (float)(Math.Cos(radian) * 2);
        float y1 = (float)(Math.Sin(radian) * 2);
        Vector3f closestCollision = GeoService.GetInstance().GetClosestCollision(effected, effected.GetX() + x1, effected.GetY() + y1, effected.GetZ());
        effect.SetTargetLoc(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ());
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.STAGGER);
    }
}
