using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Skillengine.Properties;

/// <summary>Java parity: skillengine/properties/TargetRangeProperty (ATracer, Yeats, Neon). Static set: Trap effectiveRange=attackRange; switch ONLYONE/AREA (knownList stream filters→LINQ Where chain: commonReqs, altitude, !flight-transporter, !trap-self, checkRange, checkGeo → add)/PARTY+PARTY_WITHPET (TemporaryPlayerTeam&lt;TeamMember&lt;Player&gt;&gt;, clear+rebuild, tryAddSummon)/POINT (knownList in targetDistance+1). Helpers checkCommonRequirements (resurrect→dead else !dead; BLINKING deny), isInsideDisablePvpZone (unused), checkRange (POINT/ineffective/effectiveDist angle BACK/front/cylinder), checkGeo (groundSkill geoZ NaN, canSee), tryAddSummon. streams→LINQ, Float.isNaN→float.IsNaN. Properties/AreaDirections/Trap red-tolerated.</summary>
public class TargetRangeProperty
{
    public static bool Set(Properties properties, Properties.ValidationResult result, Creature skillEffector, SkillTemplate skillTemplate, float x,
        float y, float z)
    {
        TargetRangeAttribute value = properties.GetTargetType();
        int effectiveRange = skillEffector is Trap ? skillEffector.GetGameStats().GetAttackRange().GetCurrent() : properties.GetEffectiveRange();

        List<Creature> effectedList = result.GetTargets();
        switch (value)
        {
            case TargetRangeAttribute.ONLYONE:
                break;
            case TargetRangeAttribute.AREA:
                int altitude = properties.GetEffectiveAltitude() != 0 ? properties.GetEffectiveAltitude() : 1;
                Creature firstTarget = result.GetFirstTarget();

                if (firstTarget == null)
                    return false;

                // Create a sorted map of the objects in knownlist and filter them properly
                foreach (Creature creature in firstTarget.GetKnownList()
                    .Where(knownObject => knownObject.Get() is Creature)
                    .Select(knownObject => (Creature)knownObject.Get())
                    .Where(creature => CheckCommonRequirements(creature, skillTemplate))
                    // .Where(creature => !(creature is Kisk && IsInsideDisablePvpZone(creature)))
                    .Where(creature => Math.Abs(firstTarget.GetZ() - creature.GetZ()) <= altitude)
                    .Where(creature => !(creature is Player player && player.IsUsingFlightTransporterOrWindstream()))
                    .Where(creature => !(skillEffector is Trap trap && trap.GetCreator() == creature)) // TODO this is a temporary hack for traps
                    .Where(creature => CheckRange(properties, skillEffector, x, y, z, creature, effectiveRange, firstTarget))
                    .Where(creature => CheckGeo(creature, result.GetFirstTarget(), skillTemplate)))
                {
                    effectedList.Add(creature);
                }
                break;
            case TargetRangeAttribute.PARTY:
            case TargetRangeAttribute.PARTY_WITHPET:
                // if only firsttarget will be affected (e.g. Bodyguard), we don't need to evaluate the whole group
                if (properties.GetTargetMaxCount() == 1 && properties.GetFirstTarget() != FirstTargetAttribute.POINT)
                    break;
                if (skillEffector is Player effector)
                {
                    TemporaryPlayerTeam<TeamMember<Player>> team;
                    if (value == TargetRangeAttribute.PARTY_WITHPET)
                    {
                        team = effector.GetCurrentTeam(); // group or whole alliance
                        TryAddSummon(effector.GetSummon(), result, skillTemplate, effectedList);
                    }
                    else
                    {
                        team = effector.GetCurrentGroup(); // group or alliance group (max 6 targets)
                    }
                    if (team != null)
                    {
                        effectedList.Clear();
                        foreach (Player member in team.GetMembers())
                        {
                            if (!member.IsOnline())
                                continue;
                            if (!CheckCommonRequirements(member, skillTemplate))
                                continue;
                            if (PositionUtil.IsInRange(effector, member, effectiveRange, false))
                            {
                                if (CheckGeo(member, result.GetFirstTarget(), skillTemplate))
                                    effectedList.Add(member);
                                if (value == TargetRangeAttribute.PARTY_WITHPET)
                                    TryAddSummon(member.GetSummon(), result, skillTemplate, effectedList);
                            }
                        }
                    }
                }
                break;
            case TargetRangeAttribute.POINT:
                foreach (Creature creature in skillEffector.GetKnownList()
                    .Where(knownObject => knownObject.Get() is Creature)
                    .Select(knownObject => (Creature)knownObject.Get())
                    .Where(creature => CheckCommonRequirements(creature, skillTemplate))
                    .Where(creature => !(creature is Trap trap) || trap.GetMaster().IsEnemy(skillEffector))
                    .Where(creature => PositionUtil.IsInRange(creature, x, y, z, properties.GetTargetDistance() + 1))
                    .Where(creature => CheckGeo(creature, result.GetFirstTarget(), skillTemplate)))
                {
                    effectedList.Add(creature);
                }
                break;
        }

        return true;
    }

    private static bool CheckCommonRequirements(Creature creature, SkillTemplate skillTemplate)
    {
        if (skillTemplate.HasResurrectEffect())
        {
            if (!creature.IsDead())
                return false;
        }
        else
        {
            if (creature.IsDead())
                return false;
        }

        // blinking state means protection is active (no interaction with creature is possible)
        if (creature.IsInVisualState(CreatureVisualState.BLINKING))
            return false;

        return true;
    }

    private static bool IsInsideDisablePvpZone(Creature creature)
    {
        if (creature.IsInsideZoneType(ZoneType.PVP))
        {
            foreach (ZoneInstance zone in creature.FindZones())
            {
                if (zone.GetZoneTemplate().GetFlags() == 0)
                    return true;
            }
        }
        return false;
    }

    private static bool CheckRange(Properties properties, Creature skillEffector, float x, float y, float z, Creature creature, int effectiveRange, Creature firstTarget)
    {
        if (properties.GetFirstTarget() == FirstTargetAttribute.POINT)
            return PositionUtil.IsInRange(x, y, z, creature.GetX(), creature.GetY(), creature.GetZ(), effectiveRange);
        if (properties.GetIneffectiveRange() > 0 && PositionUtil.IsInRange(firstTarget, creature, properties.GetIneffectiveRange(), false))
            return false;
        if (properties.GetEffectiveDist() > 0)
        {
            if (properties.GetEffectiveAngle() > 0)
            {
                if (creature.Equals(skillEffector))
                    return false;
                // for target_range_area_type = firestorm
                if (properties.GetEffectiveAngle() < 360)
                {
                    float angle = properties.GetEffectiveAngle() / 2f; // e.g. 60 degrees (always positive) = 30 degrees in positive and negative direction
                    if (properties.GetDirection() == AreaDirections.BACK)
                    {
                        if (!PositionUtil.IsBehind(creature, skillEffector, angle))
                            return false;
                    }
                    else if (!PositionUtil.IsInFrontOf(creature, skillEffector, angle))
                    {
                        return false;
                    }
                }
                return PositionUtil.IsInRange(skillEffector, creature, properties.GetEffectiveDist(), false);
            }
            else
            {
                // Lightning bolt
                return PositionUtil.IsInsideAttackCylinder(skillEffector, creature, properties.GetEffectiveDist(), (effectiveRange / 2f), properties.GetDirection());
            }
        }
        return PositionUtil.IsInRange(firstTarget, creature, effectiveRange, false);
    }

    private static bool CheckGeo(VisibleObject obj, Creature firstTarget, SkillTemplate skillTemplate)
    {
        // If creature is at least 2 meters above the terrain, ground skill cannot be applied
        if (GeoDataConfig.GEO_ENABLE)
        {
            if (skillTemplate.IsGroundSkill())
            {
                float geoZ = GeoService.GetInstance().GetZ(obj, obj.GetZ() + 2, obj.GetZ() - 2);
                if (float.IsNaN(geoZ))
                    return false;
            }
            if (skillTemplate.GetProperties().GetFirstTarget() != FirstTargetAttribute.POINT && !GeoService.GetInstance().CanSee(firstTarget, obj))
                return false;
        }
        return true;
    }

    private static void TryAddSummon(Summon summon, Properties.ValidationResult result, SkillTemplate skillTemplate, List<Creature> effectedList)
    {
        if (summon != null && CheckGeo(summon, result.GetFirstTarget(), skillTemplate))
            effectedList.Add(summon);
    }
}
