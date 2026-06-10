using System;
using System.Xml.Serialization;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/StumbleEffect (ATracer) : EffectTemplate. Like Stagger but also removeStunEffects; calculate: 4-state guard, STUMBLE_RESISTANCE+SpellStatus.STUMBLE, subEffect non-Player→SubEffectType.STUMBLE, 2m backward getClosestCollision/setTargetLoc; startEffect set STUMBLE + World.updatePosition + Player SM_FORCED_MOVE; endEffect→unset. Math.toRadians→*PI/180. red-tolerated.</summary>
[XmlType("StumbleEffect")]
public class StumbleEffect : EffectTemplate
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
        effected.GetEffectController().RemoveStunEffects();
        if (effected is Player player)
        {
            player.GetFlyController().OnStopGliding();
            player.GetController().OnStopMove();
        }
        World.GetInstance().UpdatePosition(effected, effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ(), effected.GetHeading());
        // TODO: FI_RobustCrash_G1 or FI_Whirlwind_G1 don't send anything, find pattern
        if (effected is Player)
            PacketSendUtility.BroadcastPacketAndReceive(effected, new SM_FORCED_MOVE(effect.GetEffector(), effected.GetObjectId(),
                    effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ()));
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.STUMBLE);
        effect.SetAbnormal(AbnormalState.STUMBLE);
    }

    public override void Calculate(Effect effect)
    {
        if (effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.OPENAERIAL)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.PULLED)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.STUMBLE)
            || effect.GetEffected().GetEffectController().IsAbnormalSet(AbnormalState.STAGGER))
        {
            return;
        }

        if (!base.Calculate(effect, StatEnum.STUMBLE_RESISTANCE, SpellStatus.STUMBLE))
            return;
        if (effect.IsSubEffect() && !(effect.GetEffected() is Player))
            effect.SetSubEffectType(SubEffectType.STUMBLE);
        Creature effector = effect.GetEffector();
        Creature effected = effect.GetEffected();
        double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(PositionUtil.GetHeadingTowards(effector, effect.GetEffected()));
        float x1 = (float)(Math.Cos(radian) * 2);
        float y1 = (float)(Math.Sin(radian) * 2);
        Vector3f closestCollision = GeoService.GetInstance().GetClosestCollision(effected, effected.GetX() + x1, effected.GetY() + y1, effected.GetZ());
        effect.SetTargetLoc(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ());
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.STUMBLE);
    }
}
