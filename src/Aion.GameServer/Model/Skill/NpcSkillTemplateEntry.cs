using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Services;
using Aion.GameServer.Skillengine.Effect;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Spawnengine;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Model.Skill;

/// <summary>
/// Java parity: model/skill/NpcSkillTemplateEntry (ATracer, nrg, Yeats). Skill entry which inherits properties
/// from template (regular npc skills). Java switch-expression on ConjunctionType/NpcSkillCondition→C# switch
/// expression; instanceof-pattern→is-pattern; Math.toRadians→*Math.PI/180; anonymous Runnable→Schedule(ct=>...);
/// currentTimeMillis→DateTimeOffset.UtcNow.ToUnixTimeMilliseconds. skillengine/SpawnEngine/GeoService/
/// TribeRelationService/PositionUtil red-tolerated.
/// </summary>
public class NpcSkillTemplateEntry : NpcSkillEntry
{
    private readonly NpcSkillTemplate template;

    public NpcSkillTemplateEntry(NpcSkillTemplate template) : base(template.GetSkillId(), template.GetSkillLevel())
    {
        this.template = template;
    }

    public override bool IsReady(int hpPercentage, long fightingTimeInMSec)
    {
        if (HasCooldown() || !ChanceReady())
            return false;

        return template.GetConjunctionType() switch
        {
            ConjunctionType.XOR => (HpReady(hpPercentage) && !TimeReady(fightingTimeInMSec)) || (!HpReady(hpPercentage) && TimeReady(fightingTimeInMSec)),
            ConjunctionType.OR => HpReady(hpPercentage) || TimeReady(fightingTimeInMSec),
            ConjunctionType.AND => HpReady(hpPercentage) && TimeReady(fightingTimeInMSec),
            _ => false,
        };
    }

    public override bool ChanceReady()
    {
        return Rnd.Chance() < template.GetProbability();
    }

    public override bool HpReady(int hpPercentage)
    {
        if (template.GetMaxhp() == 100 && template.GetMinhp() == 0) // it's not about hp
            return true;
        else if (template.GetMaxhp() >= hpPercentage && template.GetMinhp() <= hpPercentage) // in hp range
            return true;
        else
            return false;
    }

    public override bool TimeReady(long elapsedFightTime)
    {
        long minTime = template.GetMinTime();
        long maxTime = template.GetMaxTime();
        return maxTime == 0 && minTime == 0 || maxTime == 0 && minTime <= elapsedFightTime || maxTime >= elapsedFightTime && minTime <= elapsedFightTime;
    }

    public override bool HasCooldown()
    {
        return template.GetCooldown() > (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastTimeUsed);
    }

    public override bool HasPostSpawnCondition()
    {
        return template.IsPostSpawn();
    }

    public override int GetPriority()
    {
        return template.GetPriority();
    }

    public override bool ConditionReady(Creature creature)
    {
        if (creature == null || creature.IsDead() || creature.GetLifeStats().IsAboutToDie())
        {
            return false;
        }
        NpcSkillConditionTemplate condTemp = GetConditionTemplate();
        if (condTemp == null)
            return true;
        VisibleObject curTarget = creature.GetTarget();
        return condTemp.GetCondType() switch
        {
            NpcSkillCondition.NONE => true,
            NpcSkillCondition.HELP_FRIEND => HelpFriend(creature, condTemp),
            NpcSkillCondition.TARGET_IS_AETHERS_HOLD => curTarget is Creature t1 && t1.GetEffectController().IsInAnyAbnormalState(AbnormalState.OPENAERIAL),
            NpcSkillCondition.TARGET_IS_STUNNED => curTarget is Creature t2 && t2.GetEffectController().IsInAnyAbnormalState(AbnormalState.STUN),
            NpcSkillCondition.TARGET_IS_IN_ANY_STUN => curTarget is Creature t3 && t3.GetEffectController().IsInAnyAbnormalState(AbnormalState.ANY_STUN),
            NpcSkillCondition.TARGET_IS_IN_STUMBLE => curTarget is Creature t4 && t4.GetEffectController().IsInAnyAbnormalState(AbnormalState.STUMBLE),
            NpcSkillCondition.TARGET_IS_SLEEPING => curTarget is Creature t5 && t5.GetEffectController().IsInAnyAbnormalState(AbnormalState.SLEEP),
            NpcSkillCondition.TARGET_IS_FLYING => curTarget is Creature t6 && t6.IsInFlyingState(),
            NpcSkillCondition.TARGET_IS_POISONED => curTarget is Creature t7 && t7.GetEffectController().IsInAnyAbnormalState(AbnormalState.POISON),
            NpcSkillCondition.TARGET_IS_BLEEDING => curTarget is Creature t8 && t8.GetEffectController().IsInAnyAbnormalState(AbnormalState.BLEED),
            NpcSkillCondition.TARGET_IS_GATE => curTarget.GetObjectTemplate() is NpcTemplate npcTemplate && npcTemplate.GetAbyssNpcType() == AbyssNpcType.DOOR,
            NpcSkillCondition.TARGET_IS_PLAYER => curTarget is Player,
            NpcSkillCondition.TARGET_IS_NPC => curTarget is Npc,
            NpcSkillCondition.TARGET_IS_MAGICAL_CLASS => curTarget is Player player1 && !player1.GetPlayerClass().IsPhysicalClass(),
            NpcSkillCondition.TARGET_IS_PHYSICAL_CLASS => curTarget is Player player2 && player2.GetPlayerClass().IsPhysicalClass(),
            NpcSkillCondition.TARGET_HAS_CARVED_SIGNET => HasCarvedSignet(curTarget, template.GetSkillTemplate(), 0),
            NpcSkillCondition.TARGET_HAS_CARVED_SIGNET_LEVEL_II => HasCarvedSignet(curTarget, template.GetSkillTemplate(), 1),
            NpcSkillCondition.TARGET_HAS_CARVED_SIGNET_LEVEL_III => HasCarvedSignet(curTarget, template.GetSkillTemplate(), 2),
            NpcSkillCondition.TARGET_HAS_CARVED_SIGNET_LEVEL_IV => HasCarvedSignet(curTarget, template.GetSkillTemplate(), 3),
            NpcSkillCondition.TARGET_HAS_CARVED_SIGNET_LEVEL_V => HasCarvedSignet(curTarget, template.GetSkillTemplate(), 4),
            NpcSkillCondition.NPC_IS_ALIVE => creature.GetWorldMapInstance().GetNpcs(condTemp.GetNpcId()).Any(npc => !npc.IsDead()),
            NpcSkillCondition.TARGET_IS_IN_RANGE => PositionUtil.IsInRange(creature, curTarget, condTemp.GetRange(), false),
            _ => false,
        };
    }

    private bool HelpFriend(Creature creature, NpcSkillConditionTemplate condTemp)
    {
        VisibleObject validTarget = creature.GetKnownList().FindObject(knownObject =>
            knownObject.IsVisible() && knownObject.Get() is Creature target && !target.IsDead() && !target.GetLifeStats().IsAboutToDie()
            && (TribeRelationService.IsSupport(creature, target) || TribeRelationService.IsFriend(creature, target))
            && target.GetLifeStats().GetHpPercentage() <= condTemp.GetHpBelow() && PositionUtil.IsInRange(creature, target, condTemp.GetRange(), false)
            && GeoService.GetInstance().CanSee(creature, target)
        );
        if (validTarget != null)
        {
            creature.SetTarget(validTarget);
            return true;
        }
        return false;
    }

    private bool HasCarvedSignet(VisibleObject curTarget, SkillTemplate skillTemp, int signetLvl)
    {
        if (skillTemp != null && curTarget is Creature && !((Creature)curTarget).IsDead()
            && !((Creature)curTarget).GetLifeStats().IsAboutToDie())
        {
            foreach (EffectTemplate effectTemp in skillTemp.GetEffects().GetEffects())
            {
                if (effectTemp is SignetBurstEffect signetEffect)
                {
                    string signet = signetEffect.GetSignet();
                    Creature target = (Creature)curTarget;
                    Effect signetEffectOnTarget = target.GetEffectController().GetAbnormalEffect(signet);
                    if (signetEffectOnTarget != null && signetEffectOnTarget.GetSkillLevel() > signetLvl)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    public override void FireOnEndCastEvents(Npc npc)
    {
        NpcSkillSpawn spawn = template.GetSpawn();
        if (spawn == null || npc.IsDead() || npc.GetLifeStats().IsAboutToDie())
            return;
        if (spawn.GetDelay() == 0)
            SpawnNpc(npc, spawn);
        else
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                if (!npc.IsDead() && !npc.GetLifeStats().IsAboutToDie())
                    SpawnNpc(npc, spawn);
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(spawn.GetDelay()));
    }

    private void SpawnNpc(Npc npc, NpcSkillSpawn spawn)
    {
        int count = spawn.GetMaxCount() > 1 ? Rnd.Get(spawn.GetMinCount(), spawn.GetMaxCount()) : spawn.GetMinCount();
        for (int i = 0; i < count; i++)
        {
            float x1 = 0;
            float y1 = 0;
            if (spawn.GetMinDistance() > 0)
            {
                double angleRadians = Rnd.NextFloat(360f) * Math.PI / 180;
                double radian = PositionUtil.ConvertHeadingToAngle(npc.GetHeading()) * Math.PI / 180 + angleRadians;
                float distance = spawn.GetMaxDistance() > 0 ? Rnd.Get(spawn.GetMinDistance(), spawn.GetMaxDistance()) : spawn.GetMinDistance();
                x1 = (float)(Math.Cos(radian) * distance);
                y1 = (float)(Math.Sin(radian) * distance);
            }
            SpawnTemplate template = SpawnEngine.NewSingleTimeSpawn(npc.GetWorldId(), spawn.GetNpcId(), npc.GetX() + x1, npc.GetY() + y1, npc.GetZ(),
                npc.GetHeading(), npc, null);
            SpawnEngine.SpawnObject(template, npc.GetInstanceId());
        }
    }

    public override NpcSkillConditionTemplate GetConditionTemplate()
    {
        return template.GetConditionTemplate();
    }

    public override bool HasCondition()
    {
        return GetConditionTemplate() != null && GetConditionTemplate().GetCondType() != NpcSkillCondition.NONE;
    }

    public override int GetNextSkillTime()
    {
        return template.GetNextSkillTime();
    }

    public override bool HasChain()
    {
        return template.GetNextChainId() > 0;
    }

    public override int GetNextChainId()
    {
        return template.GetNextChainId();
    }

    public override int GetChainId()
    {
        return template.GetChainId();
    }

    public override NpcSkillTemplate GetTemplate()
    {
        return template;
    }

    public override bool CanUseNextChain(Npc owner)
    {
        return owner != null && (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - owner.GetGameStats().GetLastSkillTime()) < template.GetMaxChainTime();
    }
}
