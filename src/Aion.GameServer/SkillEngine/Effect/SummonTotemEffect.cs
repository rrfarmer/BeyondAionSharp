using System;
using System.Xml.Serialization;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/SummonTotemEffect (kecimis) : SummonServantEffect. applyEffect: firstTargetSelf→getClosestCollision offset (Math.toRadians→*PI/180); else pointSkill→effect x/y/z fallback to effector; group PR_PROVOKESERVENT→20s / FI_WARFLAG→15s; spawnServant(TOTEM). Vector3f/NpcObjectType/CollisionIntention red-tolerated.</summary>
[XmlType("SummonTotemEffect")]
public class SummonTotemEffect : SummonServantEffect
{
    public override void ApplyEffect(Effect effect)
    {
        Creature effector = effect.GetEffector();
        float x = effector.GetX();
        float y = effector.GetY();
        float z = effector.GetZ();
        if (effect.GetSkill().IsFirstTargetSelf())
        {
            Creature effected = effect.GetEffected();
            double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(effect.GetEffector().GetHeading());
            Vector3f pos = GeoService.GetInstance().GetClosestCollision(effector, effected.GetX() + (float)(Math.Cos(radian) * 2), effected.GetY() + (float)(Math.Sin(radian) * 2),
                    effected.GetZ(), true, CollisionIntention.DEFAULT_COLLISIONS.GetId(), IgnoreProperties.Of(effector.GetRace()));
            x = pos.GetX();
            y = pos.GetY();
            z = pos.GetZ();
        }
        else if (effect.GetSkill().IsPointSkill())
        { // fix for [657]Battle Banner
            x = effect.GetX();
            y = effect.GetY();
            z = effect.GetZ();
            if (x == 0 && y == 0)
            {
                x = effector.GetX();
                y = effector.GetY();
                z = effector.GetZ();
            }
        }
        int spawnDuration = time;
        string group = effect.GetSkillTemplate().GetGroup();
        if (group != null && group.Equals("PR_PROVOKESERVENT"))
        {
            spawnDuration = 20; // Taunting Spirit should stay 20s but the client says only 15s
        }
        else if (group != null && group.Equals("FI_WARFLAG"))
        {
            spawnDuration = 15; // same here Battle Banner 7s -> 15s
        }
        SpawnServant(effect, spawnDuration, NpcObjectType.TOTEM, x, y, z);
    }
}
