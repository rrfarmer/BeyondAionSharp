using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Effect;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Properties;

/// <summary>Java parity: skillengine/properties/FirstTargetRangeProperty (ATracer). Static set(skill, properties, castState): skip if !firstTargetRangeCheck; POINT→canSee point or STR_SKILL_OBSTACLE; null/self short-circuits; NPC mid-cast skip; CAST_END pvp→+revisionDistance; addWeaponRange→+attackRange/1000f; in-move+not-hating→+calculateMaxCoveredDistance(50); CANT_MOVE_STATE/isInAttackRange→STR_SKILL_NOT_ENOUGH_DISTANCE; canSee→STR_SKILL_OBSTACLE. CastState/IgnoreProperties red-tolerated.</summary>
public class FirstTargetRangeProperty
{
    public static bool Set(Skill skill, Properties properties, Properties.CastState castState)
    {
        float firstTargetRange = properties.GetFirstTargetRange();
        if (!skill.IsFirstTargetRangeCheck())
            return true;

        Creature effector = skill.GetEffector();
        Creature firstTarget = skill.GetFirstTarget();

        if (properties.GetFirstTarget() == FirstTargetAttribute.POINT)
        {
            if (!GeoService.GetInstance().CanSee(effector, skill.GetX(), skill.GetY(), skill.GetZ(), IgnoreProperties.Of(effector.GetRace())))
            {
                if (effector is Player)
                {
                    PacketSendUtility.SendPacket((Player)effector, SM_SYSTEM_MESSAGE.STR_SKILL_OBSTACLE());
                }
                return false;
            }
            return true;
        }

        if (firstTarget == null)
            return false;

        if (firstTarget.Equals(effector))
            return true;

        if (castState != Properties.CastState.CAST_START && !(effector is Player)) // NPCs don't cancel skills once started, could be abused -> no range or geo to check
            return true;

        // on end cast check add revision distance value (only for pvp targets, checked on 4.6 PTS)
        if (castState == Properties.CastState.CAST_END && firstTarget.GetMaster() is Player)
        {
            firstTargetRange += properties.GetRevisionDistance();
        }

        // Add Weapon Range to distance
        if (properties.IsAddWeaponRange())
            firstTargetRange += effector.GetGameStats().GetAttackRange().GetCurrent() / 1000f;

        // fixes first hit sometimes incorrectly not going through
        if (effector.GetMoveController().IsInMove() && !firstTarget.GetAggroList().IsHating(effector))
            firstTargetRange += PositionUtil.CalculateMaxCoveredDistance(effector, 50);

        if (!firstTarget.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_MOVE_STATE)
            && !PositionUtil.IsInAttackRange(effector, firstTarget, firstTargetRange))
        {
            if (effector is Player)
                PacketSendUtility.SendPacket((Player)effector, SM_SYSTEM_MESSAGE.STR_SKILL_NOT_ENOUGH_DISTANCE());
            return false;
        }

        // TODO check for all targets too
        if (!GeoService.GetInstance().CanSee(effector, firstTarget))
        {
            if (effector is Player)
            {
                PacketSendUtility.SendPacket((Player)effector, SM_SYSTEM_MESSAGE.STR_SKILL_OBSTACLE());
            }
            return false;
        }
        return true;
    }
}
