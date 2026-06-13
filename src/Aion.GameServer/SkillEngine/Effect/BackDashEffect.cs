using System.Xml.Serialization;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/BackDashEffect (ATracer) : DamageEffect. @XmlAttribute(name="distance") float; calculate override: setDashStatus(BACKDASH); getHeadingTowards; inverseAngle=convertHeadingToAngle+180; findMovementCollision; setTargetPosition; World.updatePosition; super.calculate. Vector3f/DashStatus/World red-tolerated.</summary>
[XmlType("BackDashEffect")]
public class BackDashEffect : DamageEffect
{
    [XmlAttribute("distance")]
    private float distance;

    public override void Calculate(Effect effect)
    {
        effect.SetDashStatus(DashStatus.BACKDASH);
        Creature effector = effect.GetEffector();
        byte h = PositionUtil.GetHeadingTowards(effector, effect.GetEffected());
        float inverseAngle = PositionUtil.ConvertHeadingToAngle(h) + 180; // flip by 180 degrees for opposite direction
        Vector3f closestCollision = GeoService.GetInstance().FindMovementCollision(effector, inverseAngle, distance);
        effect.GetSkill().SetTargetPosition(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ(), (sbyte)h);
        Aion.GameServer.World.World.GetInstance().UpdatePosition(effector, closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ(), h);
        base.Calculate(effect);
    }
}
