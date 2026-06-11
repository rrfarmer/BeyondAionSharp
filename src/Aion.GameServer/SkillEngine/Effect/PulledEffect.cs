using System;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/PulledEffect (Sarynth, Wakizashi, Sippolo) : EffectTemplate. applyEffect→addToEffectedController; calculate: PULLED/STUMBLE/OPENAERIAL guard, canSee guard, PULLED_RESISTANCE, subEffect→PULL/PULL_NPC, reflected→originalEffected, pull to 1.5m from effector via getClosestCollision/setTargetLoc; startEffect: !reflected→cancelCurrentSkill+Player glide/move stop, World.updatePosition, Player SM_FORCED_MOVE, set PULLED; endEffect→unset. Math.toRadians→*PI/180. red-tolerated.</summary>
[XmlType("PulledEffect")]
public class PulledEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        EffectController ec = effect.GetEffected().GetEffectController();
        if (ec.IsAbnormalSet(AbnormalState.PULLED) || ec.IsAbnormalSet(AbnormalState.STUMBLE) || ec.IsAbnormalSet(AbnormalState.OPENAERIAL))
            return;

        if (!GeoService.GetInstance().CanSee(effect.GetEffected(), effect.GetEffector()))
        {
            return;
        }
        if (!base.Calculate(effect, StatEnum.PULLED_RESISTANCE, null))
            return;
        if (effect.IsSubEffect())
            effect.SetSubEffectType(effect.GetEffected() is Player ? SubEffectType.PULL : SubEffectType.PULL_NPC);
        Creature effector = effect.IsReflected() ? effect.GetOriginalEffected() : effect.GetEffector();
        // Target must be pulled just one meter away from effector, not IN place of effector
        double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(PositionUtil.GetHeadingTowards(effector, effect.GetEffected()));
        float z = effector.GetZ();
        float x1 = (float)Math.Cos(radian) * 1.5f;
        float y1 = (float)Math.Sin(radian) * 1.5f;
        Vector3f closestCollision = GeoService.GetInstance().GetClosestCollision(effect.GetEffected(), effector.GetX() + x1, effector.GetY() + y1, z);
        effect.SetTargetLoc(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ());
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        if (!effect.IsReflected())
        {
            effected.GetController().CancelCurrentSkill(effect.GetEffector());
            if (effected is Player player)
            {
                player.GetFlyController().OnStopGliding();
                player.GetController().OnStopMove();
            }
        }
        World.GetInstance().UpdatePosition(effected, effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ(), effected.GetHeading());
        if (effected is Player)
            PacketSendUtility.BroadcastPacketAndReceive(effected,
                    new SM_FORCED_MOVE(effect.IsReflected() ? effect.GetOriginalEffected() : effect.GetEffector(), effected.GetObjectId(), effect.GetTargetX(),
                            effect.GetTargetY(), effect.GetTargetZ()));
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.PULLED);
        effect.SetAbnormal(AbnormalState.PULLED);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.PULLED);
    }
}
