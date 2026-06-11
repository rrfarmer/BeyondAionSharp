using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Skillengine.Properties;

namespace Aion.GameServer.Skillengine.Model;

/// <summary>Java parity: skillengine/model/ChargeSkill (Cheatkiller) : Skill. ctor super(...,startSkill.firstTarget,null) + motionId + setClientHitTime/CastStartTime/CastSpeed from startSkill; useSkill: !canUseSkill(CAST_END)→cancelCurrentSkill false; notify boost/startCast observers, setCasting, attach moveListener, hitTimeBoost carry (currentTimeMillis→UtcNow.ToUnixTimeMilliseconds +100), updateHitTime(CHECK_ANIMATIONS), endCast. CastState/Skill base red-tolerated.</summary>
public class ChargeSkill : Skill
{
    private readonly int motionId;

    public ChargeSkill(SkillTemplate skillTemplate, Creature effector, int skillLevel, int motionId, Skill startSkill)
        : base(skillTemplate, effector, skillLevel, startSkill.GetFirstTarget(), null)
    {
        this.motionId = motionId;
        SetClientHitTime(startSkill.GetHitTime());
        SetCastStartTime(startSkill.GetCastStartTime());
        SetCastSpeedForAnimationBoostAndChargeSkills(startSkill.GetCastSpeedForAnimationBoostAndChargeSkills());
    }

    public int GetMotionId()
    {
        return motionId;
    }

    public override bool UseSkill()
    {
        if (!CanUseSkill(Properties.Properties.CastState.CAST_END))
        {
            effector.GetController().CancelCurrentSkill(null);
            return false;
        }
        effector.GetObserveController().NotifyBoostSkillCostObservers(this);
        effector.GetObserveController().NotifyStartSkillCastObservers(this);
        effector.SetCasting(this);
        effector.GetObserveController().Attach(moveListener);
        // motion boost state from the charge starting time must not get lost
        if (effector is Player player && player.IsHitTimeBoosted(GetCastStartTime()))
            player.SetHitTimeBoost(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 100, player.GetHitTimeBoostCastSpeed());
        UpdateHitTime(SecurityConfig.CHECK_ANIMATIONS);
        EndCast();
        return true;
    }
}
