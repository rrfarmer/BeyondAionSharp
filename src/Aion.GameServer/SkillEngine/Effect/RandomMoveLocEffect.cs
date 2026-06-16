using System.Xml.Serialization;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/RandomMoveLocEffect (Bio) : EffectTemplate. @XmlAttribute(name="distance"/"direction"/"reserved5"); applyEffect: World.updatePosition to skill x/y/z/h, PlayerMoveController→setHasMovedByRandomMoveLocEffect; calculate: addSuccessEffect, DashStatus RANDOMMOVELOC_NEW/RANDOMMOVELOC by reserved5, findMovementCollision (direction==1→dir+180), setTargetPosition. Vector3f/DashStatus/PlayerMoveController red-tolerated.</summary>
[XmlType("RandomMoveLocEffect")]
public class RandomMoveLocEffect : EffectTemplate
{
    [XmlAttribute("distance")]
    public float distance;
    [XmlAttribute("direction")]
    public float direction;
    [XmlAttribute("reserved5")]
    public int reserved5;

    public override void ApplyEffect(Effect effect)
    {
        Skill skill = effect.GetSkill();
        Aion.GameServer.World.World.GetInstance().UpdatePosition(effect.GetEffector(), skill.GetX(), skill.GetY(), skill.GetZ(), (byte)skill.GetH());
        if (effect.GetEffector().GetMoveController() is PlayerMoveController pmc)
            pmc.SetHasMovedByRandomMoveLocEffect(skill);
    }

    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
        DashStatus ds = reserved5 == 1 ? DashStatus.RANDOMMOVELOC_NEW : DashStatus.RANDOMMOVELOC;
        effect.SetDashStatus(ds);

        Creature effector = effect.GetEffector();
        // Move Effector backwards direction=1 or frontwards direction=0
        float dir = PositionUtil.ConvertHeadingToAngle(effector.GetHeading());
        Vector3f closestCollision = GeoService.GetInstance().FindMovementCollision(effector, direction == 1 ? dir + 180 : dir, distance);
        effect.GetSkill().SetTargetPosition(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ(), (sbyte)effector.GetHeading());
    }
}
