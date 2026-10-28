using System;
using System.Xml.Serialization;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;
using Aion.GameServer.GeoEngine.RetailPath;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/StaggerEffect (ATracer) : EffectTemplate. applyEffect→addToEffectedController; startEffect: cancelCurrentSkill, removeParalyzeEffects, Player glide/move stop, World.updatePosition to targetLoc, Player→SM_FORCED_MOVE, set STAGGER; calculate: 4-state guard, STAGGER_RESISTANCE+SpellStatus.STAGGER, subEffect non-Player→SubEffectType.STAGGER, 2m backward via getClosestCollision, setTargetLoc; endEffect→unset. Math.toRadians→*PI/180. red-tolerated.</summary>
[XmlType("StaggerEffect")]
public class StaggerEffect : EffectTemplate
{
    private static readonly ILogger retailLog=AionLog.For(nameof(StaggerEffect));
    public override void ApplyEffect(Effect effect)
    {
        if(SkyTempleNpcForcedMove.Owns(effect)&&!SkyTempleNpcForcedMove.CanApplyAtHit(effect,out _,out _,out var failure))
        {effect.GetSuccessEffects().Remove(this);retailLog.LogWarning("SkyTemple Stagger rejected: {Reason}",failure);return;}
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        bool prepassed=false;
        if(SkyTempleNpcForcedMove.Owns(effect))
        {
            if(!SkyTempleNpcForcedMove.CanApplyAtHit(effect,out var endpoint,out prepassed,out var failure))
            {effect.GetSuccessEffects().Remove(this);retailLog.LogWarning("SkyTemple Stagger retired before commit: {Reason}",failure);return;}
            if(!prepassed)effect.SetTargetLoc(endpoint.X,endpoint.Y,endpoint.Z);
        }
        Creature effected = effect.GetEffected();
        effected.GetController().CancelCurrentSkill(effect.GetEffector());
        effected.GetEffectController().RemoveParalyzeEffects();
        if (effected is Player player)
        {
            player.GetFlyController().OnStopGliding();
            player.GetController().OnStopMove();
        }
        if(SkyTempleNpcForcedMove.Owns(effect)&&!SkyTempleNpcForcedMove.CanApplyAtHit(effect,out _,out _,out var retired))
        {effect.GetSuccessEffects().Remove(this);retailLog.LogWarning("SkyTemple Stagger retired during stop callbacks: {Reason}",retired);return;}
        if(!prepassed)
        {
            Aion.GameServer.World.World.GetInstance().UpdatePosition(effected, effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ(), effected.GetHeading());
            if(SkyTempleNpcForcedMove.Owns(effect)&&(!SkyTempleNpcForcedMove.CanCommit(effect,out _,out var declined)||
                effected.GetX()!=effect.GetTargetX()||effected.GetY()!=effect.GetTargetY()||effected.GetZ()!=effect.GetTargetZ()))
            {effect.GetSuccessEffects().Remove(this);retailLog.LogWarning("SkyTemple Stagger world commit declined: {Reason}",declined);return;}
            if (effected is Player)
                PacketSendUtility.BroadcastPacketAndReceive(effected, new SM_FORCED_MOVE(effect.GetEffector(), effected.GetObjectId(),
                        effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ()));
        }
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
        if(SkyTempleNpcForcedMove.TryCalculate(effect,out var forced))
        {
            if(!forced.Supported)
            {effect.GetSuccessEffects().Remove(this);retailLog.LogWarning("SkyTemple Stagger geometry rejected: {Reason}",forced.Detail);return;}
            var endpoint=forced.Endpoint!.Value;effect.SetTargetLoc(endpoint.X,endpoint.Y,endpoint.Z);return;
        }
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
