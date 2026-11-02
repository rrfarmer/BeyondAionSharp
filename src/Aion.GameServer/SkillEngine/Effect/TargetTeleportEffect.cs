using System;
using Aion.GameServer.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/TargetTeleportEffect (Rolandas) : EffectTemplate. @XmlAttribute(name="alias_location")→loc; applyEffect: loc==null→move next to the effector on the target's side facing him (reflected→originalEffected), Math.toRadians→*PI/180, getClosestCollision; else→SKILL_ALIAS_LOCATION_DATA + Rnd.get random position with its heading and the ground Z. SkillAliasLocation/Position red-tolerated.</summary>
[XmlType("TargetTeleportEffect")]
public class TargetTeleportEffect : EffectTemplate
{
    [XmlAttribute("alias_location")]
    public string loc;

    public override void ApplyEffect(Effect effect)
    {
        if (!(effect.GetEffected() is Player player))
            return;
        if (loc == null)
        { // teleport next to the effector, on the side of the target, facing the effector
            Creature effector = effect.IsReflected() ? effect.GetOriginalEffected() : effect.GetEffector();
            double radian = Math.PI / 180 * PositionUtil.CalculateAngleFrom(effector, player);
            float x = effector.GetX() + (float)Math.Cos(radian);
            float y = effector.GetY() + (float)Math.Sin(radian);
            Vector3f closestCollision = GeoService.GetInstance().GetClosestCollision(player, x, y, effector.GetZ());
            byte heading = PositionUtil.GetHeadingTowards(closestCollision.GetX(), closestCollision.GetY(), effector.GetX(), effector.GetY());
            MoveToPosition(player, closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ(), heading);
        }
        else
        { // teleport to random specified position
            SkillAliasLocation skillAliasLocation = DataManager.SKILL_ALIAS_LOCATION_DATA.GetSkillAliasLocation(loc);
            if (skillAliasLocation != null && player.GetWorldId() == skillAliasLocation.GetWorldId())
            {
                SkillAliasPosition position = Rnd.Get(skillAliasLocation.GetSkillAliasPositionList());
                float z = GeoService.GetInstance().GetZ(player.GetWorldId(), position.GetX(), position.GetY(), position.GetZ(), 0, player.GetInstanceId());
                if (float.IsNaN(z)) // no ground below the point
                    z = position.GetZ();
                MoveToPosition(player, position.GetX(), position.GetY(), z, position.GetHeading());
            }
        }
    }

    /// <summary>
    /// Differs from retail: retail spawns the player anew at the destination, which deselects the player's target. Moving in place keeps the target
    /// and the camera and lets the skill animation play out. To restore the retail behaviour, respawn the player on the same map without starting
    /// spawn protection.
    /// </summary>
    private void MoveToPosition(Player player, float x, float y, float z, byte heading)
    {
        player.GetController().CancelCurrentSkill(null);
        player.GetMoveController().AbortMove();
        Aion.GameServer.World.World.GetInstance().UpdatePosition(player, x, y, z, heading);
        PacketSendUtility.BroadcastToSightedPlayers(player, new SM_POSITION(player), true);
    }
}
