using System;
using System.Xml.Serialization;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/MoveBehindEffect (Sarynth, Bobobear) : DamageEffect. calculate override: setDashStatus(MOVEBEHIND); Math.toRadians(effected.heading)→*PI/180; boundRadius.getMaxOfFrontAndSide; cos/sin(PI+radian)*distance; getClosestCollision; getHeadingTowards; World.updatePosition; setTargetPosition (SM_CASTSPELL_RESULT); super.calculate. Vector3f/DashStatus/World red-tolerated.</summary>
[XmlType("MoveBehindEffect")]
public class MoveBehindEffect : DamageEffect
{
    public override void Calculate(Effect effect)
    {
        effect.SetDashStatus(DashStatus.MOVEBEHIND);
        Creature effector = effect.GetEffector();
        Creature effected = effect.GetEffected();
        double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(effected.GetHeading());
        float distance = effector.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide() + effected.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide() + 1;
        float x1 = (float)Math.Cos(Math.PI + radian) * distance;
        float y1 = (float)Math.Sin(Math.PI + radian) * distance;
        Vector3f closestCollision = GeoService.GetInstance().GetClosestCollision(effector, effected.GetX() + x1, effected.GetY() + y1, effected.GetZ());
        byte h = PositionUtil.GetHeadingTowards(effector, effected);
        World.GetInstance().UpdatePosition(effector, closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ(), h);
        // set target position for SM_CASTSPELL_RESULT
        effect.GetSkill().SetTargetPosition(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ(), h);
        base.Calculate(effect);
    }
}
