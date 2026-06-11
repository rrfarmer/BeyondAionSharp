using System;
using Aion.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/TargetTeleportEffect (Rolandas) : EffectTemplate. @XmlAttribute(name="alias_location")→loc; @XmlAttribute distance; applyEffect: Player p; loc==null→teleport in front of effector (reflected→originalEffected), Math.toRadians→*PI/180, getClosestCollision, TeleportService.teleportTo; else→SKILL_ALIAS_LOCATION_DATA + Rnd.get random position, teleportTo. SkillAliasLocation/Position/TeleportService red-tolerated.</summary>
[XmlType("TargetTeleportEffect")]
public class TargetTeleportEffect : EffectTemplate
{
    [XmlAttribute("alias_location")]
    protected string loc;

    [XmlAttribute]
    protected int distance; // TODO: find out what this value does. Its not the distance.

    public override void ApplyEffect(Effect effect)
    {
        if (effect.GetEffected() is Player p)
        {
            if (loc == null)
            { // teleport in front of effector
                Creature effector = effect.IsReflected() ? effect.GetOriginalEffected() : effect.GetEffector();
                double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(effector.GetHeading());
                float z = effector.GetZ();
                float x1 = (float)Math.Cos(radian);
                float y1 = (float)Math.Sin(radian);
                Vector3f closestCollision = GeoService.GetInstance().GetClosestCollision(effect.GetEffected(), effector.GetX() + x1, effector.GetY() + y1, z);
                TeleportService.TeleportTo(p, p.GetWorldId(), closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ());
            }
            else
            { // teleport to random specified position
                SkillAliasLocation skillAliasLocation = DataManager.SKILL_ALIAS_LOCATION_DATA.GetSkillAliasLocation(loc);
                if (skillAliasLocation != null && p.GetWorldId() == skillAliasLocation.GetWorldId())
                {
                    SkillAliasPosition position = Rnd.Get(skillAliasLocation.GetSkillAliasPositionList());
                    TeleportService.TeleportTo(p, p.GetWorldId(), position.GetX(), position.GetY(), position.GetZ());
                }
            }
        }
    }
}
