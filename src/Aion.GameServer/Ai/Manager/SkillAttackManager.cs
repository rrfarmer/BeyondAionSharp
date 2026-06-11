using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.Commons.Utils;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.SkillEngine.Effect;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Ai.Manager;

/// <summary>
/// Java parity: ai/manager/SkillAttackManager (ATracer, Yeats). Performs/chooses NPC skill attacks. switch-expression on
/// NpcSkillTargetAttribute -> C# switch expr (+ _ discard); Collections.shuffle -> in-place Fisher-Yates via Rnd; Comparator.reversed
/// -> descending Sort; stream.anyMatch -> LINQ Any; Integer.MAX_VALUE -> int.MaxValue; currentTimeMillis -> UtcNow.ToUnixTimeMilliseconds;
/// instanceof+pattern -> is. AiSubState.Cast/None; AiState.Fight; SkillType/AbnormalState/AggroTarget/FirstTargetAttribute SCREAMING.
/// DataManager/SkillTemplate/Properties red-tolerated.
/// </summary>
public class SkillAttackManager
{
    public static void PerformAttack(NpcAI npcAI, int delay)
    {
        if (npcAI.GetOwner().GetObjectTemplate().GetAttackRange() == 0)
        {
            if (npcAI.GetOwner().GetTarget() != null
                && !PositionUtil.IsInRange(npcAI.GetOwner(), npcAI.GetOwner().GetTarget(), npcAI.GetOwner().GetAggroRange()))
            {
                npcAI.GetOwner().GetController().AbortCast();
                npcAI.OnGeneralEvent(AiEventType.TargetToofar);
                return;
            }
        }
        if (npcAI.SetSubStateIfNot(AiSubState.Cast))
        {
            if (delay > 0)
            {
                ThreadPoolManager.GetInstance().Schedule(ct => { SkillAction(npcAI); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(delay));
            }
            else
            {
                SkillAction(npcAI);
            }
        }
    }

    protected static void SkillAction(NpcAI npcAI)
    {
        if (npcAI.GetSubState() != AiSubState.Cast)
        {
            if (npcAI.GetSubState() == AiSubState.None && npcAI.GetState() == AiState.Fight) // cast was interrupted, so resume attacking
                npcAI.Think();
            return;
        }
        Npc owner = npcAI.GetOwner();
        VisibleObject target = owner.GetTarget();
        NpcSkillEntry skill = owner.GetGameStats().GetLastSkill();
        if (!(target is Creature creature) || creature.IsDead() || skill == null)
        {
            npcAI.SetSubStateIfNot(AiSubState.None);
            npcAI.OnGeneralEvent(AiEventType.TargetGiveup);
            return;
        }
        if (owner.GetObjectTemplate().GetAttackRange() == 0 && !PositionUtil.IsInRange(owner, target, owner.GetAggroRange()))
        {
            owner.GetController().AbortCast();
            npcAI.OnGeneralEvent(AiEventType.TargetToofar);
            return;
        }
        SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(skill.GetSkillId());
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "Using skill " + skill.GetSkillId() + " level: " + skill.GetSkillLevel() + " duration: " + template.GetDuration());
        }
        if ((template.GetSkillType() == SkillType.MAGICAL && owner.GetEffectController().IsAbnormalSet(AbnormalState.SILENCE))
            || (template.GetSkillType() == SkillType.PHYSICAL && owner.GetEffectController().IsAbnormalSet(AbnormalState.BIND))
            || (owner.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE))
            || (owner.IsTransformed() && owner.GetTransformModel().GetBanUseSkills() == 1))
        {
            AfterUseSkill(npcAI);
        }
        else
        {
            if (template.GetProperties().GetFirstTarget() == FirstTargetAttribute.ME)
            {
                owner.SetTarget(owner);
            }
            else
            {
                NpcSkillTemplate temp = skill.GetTemplate();
                int range = template.GetProperties().GetFirstTargetRange() == 0 ? int.MaxValue : template.GetProperties().GetFirstTargetRange();
                VisibleObject newTarget = temp.GetTarget() switch
                {
                    NpcSkillTargetAttribute.FRIEND => owner.GetKnownList().FindObject(o => o.IsVisible() && o.Get() is Npc npc && !npc.IsDead() && !npc.GetLifeStats().IsAboutToDie() && !owner.IsEnemy(npc)
                            && PositionUtil.IsInRange(owner, npc, range, false) && GeoService.GetInstance().CanSee(owner, npc)),
                    NpcSkillTargetAttribute.ME => owner,
                    NpcSkillTargetAttribute.MOST_HATED => owner.GetAggroList().GetTarget(AggroTarget.MOST_HATED),
                    NpcSkillTargetAttribute.SECOND_MOST_HATED => owner.GetAggroList().GetTarget(AggroTarget.SECOND_MOST_HATED),
                    NpcSkillTargetAttribute.THIRD_MOST_HATED => owner.GetAggroList().GetTarget(AggroTarget.THIRD_MOST_HATED),
                    NpcSkillTargetAttribute.RANDOM => owner.GetAggroList().GetTarget(AggroTarget.RANDOM, range),
                    NpcSkillTargetAttribute.RANDOM_EXCEPT_CURRENT_TARGET => owner.GetAggroList().GetTarget(AggroTarget.RANDOM_EXCEPT_CURRENT_TARGET, range),
                    NpcSkillTargetAttribute.NONE => null,
                    _ => null,
                };
                if (newTarget != null)
                    owner.SetTarget(newTarget);
            }
            bool success = owner.GetController().UseSkill(skill.GetSkillId(), skill.GetSkillLevel());
            if (!success)
            {
                AfterUseSkill(npcAI);
            }
        }
    }

    public static void AfterUseSkill(NpcAI npcAI)
    {
        npcAI.SetSubStateIfNot(AiSubState.None);
        npcAI.OnGeneralEvent(AiEventType.AttackComplete);
    }

    public static NpcSkillEntry ChooseNextSkill(NpcAI npcAI)
    {
        if (npcAI.IsInSubState(AiSubState.Cast))
        {
            return null;
        }

        Npc owner = npcAI.GetOwner();

        NpcSkillEntry queuedSkill = owner.GetNextQueuedSkill();
        if (queuedSkill != null && queuedSkill.GetNextSkillTime() == 0 && IsReady(owner, queuedSkill))
        {
            return GetNpcSkillEntryIfNotTooFarAway(owner, queuedSkill);
        }

        if (((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - owner.GetGameStats().GetFightStartingTime()) > owner.GetGameStats().GetInitialSkillDelay())
            && owner.GetGameStats().CanUseNextSkill())
        {
            if (queuedSkill != null && IsReady(owner, queuedSkill))
            {
                return GetNpcSkillEntryIfNotTooFarAway(owner, queuedSkill);
            }

            NpcSkillList skillList = owner.GetSkillList();
            if (skillList.IsEmpty())
            {
                return null;
            }

            NpcSkillEntry lastSkill = owner.GetGameStats().GetLastSkill();
            if (lastSkill != null && lastSkill.HasChain() && lastSkill.CanUseNextChain(owner))
            {
                List<NpcSkillEntry> chainSkills = skillList.GetChainSkills(lastSkill);
                if (chainSkills.Count > 1)
                {
                    if (chainSkills.Any(cs => cs.GetPriority() > 0))
                    {
                        chainSkills.Sort((a, b) => b.GetPriority().CompareTo(a.GetPriority()));
                    }
                    else
                    {
                        Shuffle(chainSkills);
                    }
                }
                foreach (NpcSkillEntry entry in chainSkills)
                {
                    if (entry != null && IsReady(owner, entry))
                    {
                        return GetNpcSkillEntryIfNotTooFarAway(owner, entry);
                    }
                }
            }

            int[] priorities = skillList.GetPriorities();
            if (priorities != null)
            {
                foreach (int priority in priorities)
                {
                    List<NpcSkillEntry> skillsByPriority = skillList.GetSkillsByPriority(priority);
                    if (skillsByPriority.Count > 1)
                        Shuffle(skillsByPriority);

                    foreach (NpcSkillEntry entry in skillsByPriority)
                    {
                        if (entry.GetChainId() == 0 && IsReady(owner, entry))
                        {
                            return GetNpcSkillEntryIfNotTooFarAway(owner, entry);
                        }
                    }
                }
            }
        }
        return null;
    }

    private static NpcSkillEntry GetNpcSkillEntryIfNotTooFarAway(Npc owner, NpcSkillEntry entry)
    {
        if (TargetTooFar(owner, entry))
        {
            owner.GetGameStats().SetNextSkillDelay(5000);
            return null;
        }
        return entry;
    }

    // check for bind/silence/fear/stun etc debuffs on npc
    private static bool IsReady(Npc owner, NpcSkillEntry entry)
    {
        if (entry.IsReady(owner.GetLifeStats().GetHpPercentage(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - owner.GetGameStats().GetFightStartingTime()))
        {
            if (entry.ConditionReady(owner))
            {
                SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(entry.GetSkillId());
                if ((template.GetSkillType() == SkillType.MAGICAL && owner.GetEffectController().IsAbnormalSet(AbnormalState.SILENCE))
                    || (template.GetSkillType() == SkillType.PHYSICAL && owner.GetEffectController().IsAbnormalSet(AbnormalState.BIND))
                    || (owner.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE))
                    || (owner.IsTransformed() && owner.GetTransformModel().GetBanUseSkills() == 1))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool TargetTooFar(Npc owner, NpcSkillEntry entry)
    {
        SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(entry.GetSkillId());
        Properties prop = template.GetProperties();
        if (prop.GetFirstTarget() != FirstTargetAttribute.ME && entry.GetTemplate().GetTarget() != NpcSkillTargetAttribute.NONE
            && entry.GetTemplate().GetTarget() != NpcSkillTargetAttribute.MOST_HATED && entry.GetTemplate().GetTarget() != NpcSkillTargetAttribute.ME)
        {
            if (owner.GetTarget() is Creature target)
            {
                if (target.IsDead() || !owner.CanSee(target))
                {
                    return true;
                }
                if (prop.GetTargetType() != TargetRangeAttribute.AREA)
                {
                    if (!PositionUtil.IsInRange(owner, target, prop.GetFirstTargetRange(), false))
                    {
                        return true;
                    }
                }
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Java parity: Collections.shuffle — in-place Fisher-Yates via Rnd.</summary>
    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Rnd.Get(0, i);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
