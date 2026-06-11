using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/MotionData (kecimis). @XmlRootElement(motion_times)→[XmlRoot]; @XmlTransient Map→[XmlIgnore] Dictionary built in AfterUnmarshal (motionTimes nulled); instanceof ChargeSkill→is; Math.max/min→Math.Max/Min; record AnimationTimes. Skill/Motion/Times/Stat2 red-tolerated.</summary>
[XmlRoot("motion_times")]
public class MotionData
{
    [XmlElement("motion_time")]
    public List<MotionTime> motionTimes;

    [XmlIgnore]
    private readonly Dictionary<string, MotionTime> motionTimesMap = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (MotionTime motion in motionTimes)
        {
            motionTimesMap[motion.GetName()] = motion;
        }
        motionTimes = null;
    }

    public ICollection<MotionTime> GetMotionTimes()
    {
        return motionTimesMap.Values;
    }

    public MotionTime GetMotionTime(string name)
    {
        return motionTimesMap.GetValueOrDefault(name);
    }

    public MotionTime GetMotionTime(Skill skill)
    {
        Motion motion = skill.GetSkillTemplate().GetMotion();
        if (motion == null || motion.GetName() == null) // instant skills like Remove Shock (283) or Feint (912)
            return null; // some skills, like Blind Side (3467) or scroll/food buffs have no motion
        return GetMotionTime(motion.GetName());
    }

    public float CalculateAnimationTimeUntilFirstHit(Player player, Skill skill)
    {
        MotionTime motionTime = GetMotionTime(skill);
        if (motionTime == null)
            return 0f;
        Times times = motionTime.GetTimesFor(player, 1);
        if (times == null)
            return 0f;
        int motionSpeed = skill.GetSkillTemplate().GetMotion().GetSpeed() * 10;
        float attackRate = GetAttackRate(player);
        float motionSpeedRate = player.IsHitTimeBoosted() ? Math.Min(attackRate, CalculateCastSpeedRate(player.GetHitTimeBoostCastSpeed())) : attackRate;
        return (player.IsInRobotMode() ? times.GetAnimationLength() : times.GetMinTime()) * motionSpeed * motionSpeedRate;
    }

    public AnimationTimes CalculateAnimationTimesAfterLastHit(Player player, Skill skill)
    {
        MotionTime motionTime = GetMotionTime(skill);
        if (motionTime == null)
            return null;
        int motionId = skill is ChargeSkill chargeSkill ? chargeSkill.GetMotionId() : Math.Max(1, skill.GetMultiCastCount());
        Times times = motionTime.GetTimesFor(player, motionId);
        if (times == null)
            return null;
        int motionSpeed = skill.GetSkillTemplate().GetMotion().GetSpeed() * 10;
        float attackRate = GetAttackRate(player);
        float motionSpeedRate = skill.AllowAnimationBoostByCastSpeed() ? Math.Min(attackRate, CalculateCastSpeedRate(skill.GetCastSpeedForAnimationBoostAndChargeSkills())) : attackRate;
        // TODO parse movement related times (see "_run" suffix in client templates)
        int animationLastHitMillis = (int)(times.GetMaxTime() * motionSpeed * motionSpeedRate);
        // TODO parse animation_length to remove approxAnimationLength (currently only parsed for AT)
        float approxAnimationLength = times.GetAnimationLength() > 0 ? times.GetAnimationLength() : 1.4f + times.GetMaxTime();
        int animationFullDurationMillis = (int)(approxAnimationLength * motionSpeed * motionSpeedRate);
        return new AnimationTimes(animationLastHitMillis, animationFullDurationMillis);
    }

    private float GetAttackRate(Player player)
    {
        Stat2 attackSpeedStat = player.GetGameStats().GetAttackSpeed();
        return attackSpeedStat.GetCurrent() / (float)attackSpeedStat.GetBase();
    }

    private float CalculateCastSpeedRate(float castSpeedForAnimationBoost)
    {
        castSpeedForAnimationBoost = Math.Max(0.5f, Math.Min(1f, castSpeedForAnimationBoost)); // these are limits enforced by the game client
        return castSpeedForAnimationBoost + (1 - castSpeedForAnimationBoost) / 2; // only half of the cast speed can affect animations
    }

    public int Size()
    {
        return motionTimesMap.Count;
    }

    public record AnimationTimes(int LastHitMillis, int FullDurationMillis);
}
