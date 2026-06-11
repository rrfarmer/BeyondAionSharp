using System;
using System.Xml.Serialization;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/DashEffect (ATracer) : DamageEffect. calculate override: only when effected==skill.firstTarget; setDashStatus(DASH); getHeadingTowards; Math.toRadians→*PI/180; boundRadius.getMaxOfFrontAndSide; cos/sin(PI+radian)*distance; getClosestCollision; setTargetPosition; World.updatePosition; super.calculate. Vector3f/DashStatus/World red-tolerated.</summary>
[XmlType("DashEffect")]
public class DashEffect : DamageEffect
{
    public override void Calculate(Effect effect)
    {
        Creature effected = effect.GetEffected();
        if (effected.Equals(effect.GetSkill().GetFirstTarget()))
        { // move only once for Dash-AoE (e.g 2705)
            effect.SetDashStatus(DashStatus.DASH);
            byte h = PositionUtil.GetHeadingTowards(effect.GetEffector(), effected);
            double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(h);
            float distance = effect.GetEffector().GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide() + effected.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide() + 1;
            float x1 = (float)Math.Cos(Math.PI + radian) * distance;
            float y1 = (float)Math.Sin(Math.PI + radian) * distance;
            Vector3f closestCollision = GeoService.GetInstance().GetClosestCollision(effect.GetEffected(), effected.GetX() + x1, effected.GetY() + y1, effected.GetZ());
            effect.GetSkill().SetTargetPosition(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ(), h);
            World.GetInstance().UpdatePosition(effect.GetEffector(), closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ(), h);
        }
        base.Calculate(effect);
    }
}
