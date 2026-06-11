using System;
using System.Collections.Generic;
using System.Threading;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.SkillEngine.Effect;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Effect = Aion.GameServer.SkillEngine.Model.Effect;

namespace Aion.GameServer.Controllers.Effect;

/// <summary>
/// Java parity: controllers/effect/EffectController (@author ATracer, Wakizashi, Sippolo, Cheatkiller, Neon).
/// </summary>
/// <remarks>
/// FIDELITY NOTE: Java uses LinkedHashMap (strict insertion order across removals). C# Dictionary preserves
/// insertion order on the common path but may diverge after removals; a dedicated ordered-map port is the
/// follow-up for exact eviction-order parity. Java StampedLock is modeled with ReaderWriterLockSlim.
/// </remarks>
public class EffectController
{
    private static readonly Dictionary<string, Effect> EMPTY_MAP = new Dictionary<string, Effect>();

    private readonly Creature owner;
    private readonly ReaderWriterLockSlim @lock = new ReaderWriterLockSlim();
    private volatile Dictionary<string, Effect> passiveEffectMap = EMPTY_MAP;
    private readonly Dictionary<string, Effect> abnormalEffectMap = new Dictionary<string, Effect>();
    private int abnormals;

    public EffectController(Creature owner)
    {
        this.owner = owner;
    }

    public virtual Creature GetOwner()
    {
        return owner;
    }

    public virtual void AddEffect(Effect nextEffect)
    {
        Dictionary<string, Effect> mapToUpdate = GetMapForEffect(nextEffect);

        bool useEffectId = true;
        if (nextEffect.IsPassive())
        {
            Effect existingEffect;
            @lock.EnterReadLock();
            try
            {
                mapToUpdate.TryGetValue(nextEffect.GetStack(), out existingEffect);
            }
            finally
            {
                @lock.ExitReadLock();
            }
            if (existingEffect != null && existingEffect.IsPassive())
            {
                // check stack level
                if (existingEffect.GetSkillStackLvl() > nextEffect.GetSkillStackLvl())
                    return;

                // check skill level (when stack level same)
                if (existingEffect.GetSkillStackLvl() == nextEffect.GetSkillStackLvl() && existingEffect.GetSkillLevel() > nextEffect.GetSkillLevel())
                    return;

                existingEffect.EndEffect();
                useEffectId = false;
            }
        }

        if (useEffectId)
        {
            // idea here is that effects with same effectId shouldn't stack, effect with higher basic lvl takes priority
            if (SearchConflict(mapToUpdate, nextEffect))
            {
                if (!nextEffect.IsPassive() && nextEffect.GetTargetSlot() != SkillTargetSlot.DEBUFF)
                    nextEffect.SetEffectResult(EffectResult.CONFLICT);
                return;
            }
        }
        EndConflictedEffect(mapToUpdate, nextEffect);
        CheckEffectCooldownId(nextEffect);

        // max 3 aura effects or 1 toggle skill in noshoweffects
        if (IsNoShowToggle(nextEffect))
        {
            int mts = nextEffect.GetSkillSubType() == SkillSubType.CHANT ? 3 : 1;
            // Rangers & Riders are allowed to use 2 Toggle skills. There might be a pattern.
            if (nextEffect.GetEffector() is Player player && (player.GetPlayerClass() == PlayerClass.RANGER
                || player.GetPlayerClass() == PlayerClass.RIDER))
            {
                mts = 2;
            }
            List<Effect> filteredMap = nextEffect.GetSkillSubType() == SkillSubType.CHANT ? GetAuraEffects() : GetNoShowToggleEffectsExceptAuras();
            if (filteredMap.Count >= mts)
                filteredMap[0].EndEffect();
        }

        // max 4 chants
        if (nextEffect.IsChant())
        {
            List<Effect> chants = FilterEffects(abnormalEffectMap, e => !IsNoShowToggle(e) && e.IsChant());
            if (chants.Count >= 4)
                chants[0].EndEffect();
        }
        Put(mapToUpdate, nextEffect);

        nextEffect.StartEffect();

        if (!nextEffect.IsPassive())
            BroadCastEffects(nextEffect);
    }

    protected void Put(Effect nextEffect)
    {
        Put(GetMapForEffect(nextEffect), nextEffect);
    }

    private void Put(Dictionary<string, Effect> mapToUpdate, Effect nextEffect)
    {
        @lock.EnterWriteLock();
        try
        {
            mapToUpdate[nextEffect.GetStack()] = nextEffect;
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    private void EndConflictedEffect(Dictionary<string, Effect> effectMap, Effect newEffect)
    {
        int conflictId = newEffect.GetSkillTemplate().GetConflictId();
        if (conflictId == 0)
            return;
        Effect effectToEnd = FindFirstEffect(effectMap, effect => effect.GetSkillTemplate().GetConflictId() == conflictId);
        if (effectToEnd != null)
            effectToEnd.EndEffect();
    }

    private bool SearchConflict(Dictionary<string, Effect> mapToUpdate, Effect nextEffect)
    {
        if (CheckExtraEffect(mapToUpdate, nextEffect))
            return false;
        Effect effectToEnd = null;
        @lock.EnterReadLock();
        try
        {
            foreach (Effect effect in mapToUpdate.Values)
            {
                if (!CanConflict(effect, nextEffect))
                    continue;
                foreach (EffectTemplate et in effect.GetEffectTemplates())
                {
                    if (et.GetEffectId() == 0)
                        continue;
                    foreach (EffectTemplate et2 in nextEffect.GetEffectTemplates())
                    {
                        if (et2.GetEffectId() == 0)
                            continue;
                        if ((et.GetEffectId() == et2.GetEffectId()) || (et is SilenceEffect && et2 is SilenceEffect))
                        {
                            if (et.GetBasicLvl() > et2.GetBasicLvl())
                            {
                                return true;
                            }
                            else
                            {
                                effectToEnd = effect;
                                goto mainLoopEnd;
                            }
                        }
                    }
                }
            }
            mainLoopEnd:;
        }
        finally
        {
            @lock.ExitReadLock();
        }
        if (effectToEnd != null)
            effectToEnd.EndEffect(effectToEnd.GetTargetSlot() != nextEffect.GetTargetSlot());
        return false;
    }

    /// <summary>True if <c>newEffect</c> is in conflict with another existing effect.</summary>
    public bool IsConflicting(Effect newEffect)
    {
        if (newEffect.IsPassive() || newEffect.GetTargetSlot() == SkillTargetSlot.DEBUFF)
            return false;
        Dictionary<string, Effect> mapForEffect = GetMapForEffect(newEffect.GetSkillTemplate(), false);
        @lock.EnterReadLock();
        try
        {
            foreach (Effect currentEffect in mapForEffect.Values)
            {
                if (!CanConflict(currentEffect, newEffect))
                    continue;
                foreach (EffectTemplate newEffectTemplate in newEffect.GetEffectTemplates())
                {
                    if (newEffectTemplate.GetEffectId() == 0)
                        continue;
                    foreach (EffectTemplate currentEffectTemplate in currentEffect.GetEffectTemplates())
                    {
                        if (currentEffectTemplate.GetEffectId() == 0)
                            continue;
                        if ((currentEffectTemplate.GetEffectId() == newEffectTemplate.GetEffectId())
                            || (currentEffectTemplate is SilenceEffect && newEffectTemplate is SilenceEffect))
                        {
                            if (currentEffectTemplate.GetBasicLvl() > newEffectTemplate.GetBasicLvl() && !(currentEffectTemplate is HideEffect))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            @lock.ExitReadLock();
        }
        return false;
    }

    private static bool CanConflict(Effect e1, Effect e2)
    {
        if (e1.GetTargetSlot() == e2.GetTargetSlot())
            return true;
        if (e1.GetSkillSubType() == e2.GetSkillSubType())
            return e1.GetTargetSlot() != SkillTargetSlot.BOOST && e2.GetTargetSlot() != SkillTargetSlot.BOOST; // retail allows Lucky Vinna II + candy
        return false;
    }

    private bool CheckExtraEffect(Dictionary<string, Effect> effectMap, Effect nextEffect)
    {
        if (nextEffect.IsPassive() || nextEffect.GetDispelCategory() != DispelCategoryType.EXTRA)
            return false;
        Effect extraEffect = FindFirstEffect(effectMap, effect => effect.GetDispelCategory() == DispelCategoryType.EXTRA
            && !effect.GetSkillTemplate().GetStack().StartsWith("IDSEAL_BOSS_VRITRA_BUFF"));
        if (extraEffect != null)
        {
            extraEffect.EndEffect();
            return true;
        }
        return false;
    }

    private List<Effect> GetAuraEffects()
    {
        return FilterEffects(abnormalEffectMap, effect => IsNoShowToggle(effect) && effect.GetSkillSubType() == SkillSubType.CHANT);
    }

    private List<Effect> GetNoShowToggleEffectsExceptAuras()
    {
        return FilterEffects(abnormalEffectMap, effect => IsNoShowToggle(effect) && effect.GetSkillSubType() != SkillSubType.CHANT);
    }

    private void CheckEffectCooldownId(Effect effect)
    {
        if (effect.IsPassive() || effect.GetTargetSlot() == SkillTargetSlot.NOSHOW)
            return;
        int cdId = effect.GetSkillTemplate().GetCooldownId();
        if (cdId == 1)
            return;
        int size = 1;
        switch (cdId)
        {
            case 273: // Erosion & Flamecage
            case 353: // Lockdown & Dazing Severe Blow
                size = 2;
                break;
            case 6:
                size = 10;
                break;
        }
        List<Effect> effects = FilterEffects(abnormalEffectMap,
            e => !IsNoShowToggle(e) && e.GetTargetSlot() == effect.GetTargetSlot() && e.GetSkillTemplate().GetCooldownId() == cdId);
        if (effects.Count >= size)
            effects[0].EndEffect();
        // archer buffs
        if (cdId >= 2020 && cdId <= 2030)
        {
            int count = 0;
            Effect toRemove = null;
            foreach (Effect eff in GetAbnormalEffectsToShow())
            {
                switch (eff.GetSkillTemplate().GetCooldownId())
                {
                    case 2020: // Dodging
                    case 2022: // Focused Shots
                    case 2024: // Aiming
                    case 2026: // Bestial Fury
                    case 2028: // Hunter's Eye
                    case 2030: // Strong Shots
                        if (toRemove == null)
                            toRemove = eff;
                        if (++count >= 2)
                        {
                            toRemove.EndEffect();
                            return;
                        }
                        break;
                }
            }
        }
    }

    private Dictionary<string, Effect> GetMapForEffect(Effect effect)
    {
        return GetMapForEffect(effect.GetSkillTemplate(), true);
    }

    private Dictionary<string, Effect> GetMapForEffect(SkillTemplate template, bool initialize)
    {
        return template.IsPassive() ? GetPassiveEffectMap(initialize) : abnormalEffectMap;
    }

    private Dictionary<string, Effect> GetPassiveEffectMap(bool initialize)
    {
        if (initialize && ReferenceEquals(passiveEffectMap, EMPTY_MAP))
        {
            lock (this)
            {
                if (ReferenceEquals(passiveEffectMap, EMPTY_MAP))
                    passiveEffectMap = new Dictionary<string, Effect>();
            }
        }
        return passiveEffectMap;
    }

    public Effect GetAbnormalEffect(string stack)
    {
        @lock.EnterReadLock();
        try
        {
            return abnormalEffectMap.TryGetValue(stack, out Effect value) ? value : null;
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public bool HasAbnormalEffect(Predicate<Effect> predicate)
    {
        return FindFirstEffect(abnormalEffectMap, predicate) != null;
    }

    public bool HasAbnormalEffect(int skillId)
    {
        return HasAbnormalEffect(effect => effect.GetSkillId() == skillId);
    }

    public bool IsUnderNormalShield()
    {
        return HasAbnormalEffect(e => (e.GetShieldDefense() & ShieldType.NORMAL.GetId()) == ShieldType.NORMAL.GetId());
    }

    public void BroadCastEffects(Effect effect)
    {
        int slot = effect != null ? effect.GetTargetSlot().GetId() : SkillTargetSlotExtensions.FULLSLOTS;
        List<Effect> effects = GetAbnormalEffects();
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_ABNORMAL_EFFECT(GetOwner(), abnormals, effects, slot));
    }

    public virtual void ClearEffect(Effect effect, bool broadCastEffects)
    {
        Dictionary<string, Effect> effectMap = GetMapForEffect(effect.GetSkillTemplate(), false);
        @lock.EnterWriteLock();
        try
        {
            if (effectMap.TryGetValue(effect.GetStack(), out Effect oldEffect) && oldEffect != null)
            {
                if (!oldEffect.Equals(effect))
                    return; // effect in map was already replaced by a newer one (e.g. when toggling many auras), so there's no need to re-broadcast
                effectMap.Remove(effect.GetStack());
            }
        }
        finally
        {
            @lock.ExitWriteLock();
        }
        if (broadCastEffects)
            BroadCastEffects(effect);
    }

    public Effect FindBySkillId(int skillId)
    {
        SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        if (skillTemplate == null)
            throw new NullReferenceException("Skill with ID " + skillId + " does not exist");
        return FindFirstEffect(GetMapForEffect(skillTemplate, false), effect => effect.GetSkillId() == skillId);
    }

    public void RemoveEffect(int skillId)
    {
        Effect effect = FindBySkillId(skillId);
        if (effect != null)
            effect.EndEffect();
    }

    public void RemoveHideEffects()
    {
        RemoveEffects(abnormalEffectMap, e => e.IsHideEffect());
    }

    public void RemovePetOrderUnSummonEffects()
    {
        RemoveEffects(abnormalEffectMap, e => e.IsPetOrderUnSummonEffect());
    }

    public void RemoveParalyzeEffects()
    {
        RemoveEffects(abnormalEffectMap, e => e.IsParalyzeEffect());
    }

    public void RemoveStunEffects()
    {
        RemoveEffects(abnormalEffectMap, e => e.IsStunEffect());
    }

    /// <summary>
    /// Removes Transform effects from owner. For more info see <see cref="TransformEffect"/>; it doesn't remove Avatar transforms (TODO find out on retail).
    /// </summary>
    public void RemoveTransformEffects()
    {
        RemoveEffects(abnormalEffectMap, effect =>
        {
            foreach (EffectTemplate et in effect.GetEffectTemplates())
            {
                if (et is TransformEffect && ((TransformEffect)et).GetTransformType() != TransformType.AVATAR)
                    return true;
            }
            return false;
        });
    }

    public void RemoveInstanceEffects()
    {
        RemoveEffects(abnormalEffectMap, effect =>
        {
            if (effect.GetDispelCategory() == DispelCategoryType.NPC_BUFF)
                return true;
            foreach (EffectTemplate et in effect.GetEffectTemplates())
            {
                if (et is TransformEffect te && te.GetTransformType() == TransformType.FORM1)
                    return true;
            }
            return false;
        });
    }

    private void RemoveEffects(Dictionary<string, Effect> mapForEffect, Predicate<Effect> predicate)
    {
        foreach (Effect effect in FilterEffects(mapForEffect, predicate))
            effect.EndEffect();
    }

    private Effect FindFirstEffect(Dictionary<string, Effect> effectMap, Predicate<Effect> filter)
    {
        @lock.EnterReadLock();
        try
        {
            foreach (Effect effect in effectMap.Values)
            {
                if (filter(effect))
                    return effect;
            }
        }
        finally
        {
            @lock.ExitReadLock();
        }
        return null;
    }

    public List<Effect> GetAllEffects()
    {
        @lock.EnterReadLock();
        try
        {
            List<Effect> effects = new List<Effect>(abnormalEffectMap.Values);
            effects.AddRange(passiveEffectMap.Values);
            return effects;
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    private List<Effect> FilterEffects(Dictionary<string, Effect> effectMap, Predicate<Effect> filter)
    {
        List<Effect> effects = new List<Effect>();
        @lock.EnterReadLock();
        try
        {
            foreach (Effect effect in effectMap.Values)
            {
                if (filter(effect))
                    effects.Add(effect);
            }
        }
        finally
        {
            @lock.ExitReadLock();
        }
        return effects;
    }

    /// <summary>Removes all effects by given dispel slot type.</summary>
    public void RemoveByDispelSlotType(DispelSlotType dispelSlotType)
    {
        RemoveByDispelEffect(null, dispelSlotType, 255, 100, 100);
    }

    public bool RemoveByEffectId(int effectId, int dispelLevel, int power)
    {
        return RemoveByEffectId(abnormalEffectMap, effectId, dispelLevel, power);
    }

    private bool RemoveByEffectId(Dictionary<string, Effect> effectMap, int effectId, int dispelLevel, int power)
    {
        Effect effectToEnd = FindFirstEffect(effectMap,
            effect => effect.GetReqDispelLevel() <= dispelLevel && effect.ContainsEffectId(effectId) && RemovePower(effect, power));
        if (effectToEnd != null)
        {
            effectToEnd.EndEffect();
            return true;
        }
        else
        {
            return false;
        }
    }

    public void RemoveByDispelEffect(EffectType? effectType, DispelSlotType? dispelSlotType, int count, int dispelLevel, int power)
    {
        RemoveByDispelEffect(abnormalEffectMap, effectType, dispelSlotType, count, dispelLevel, power);
    }

    private int RemoveByDispelEffect(Dictionary<string, Effect> effectMap, EffectType? effectType, DispelSlotType? dispelSlotType, int count, int dispelLevel,
        int power)
    {
        List<Effect> effectsToEnd = new List<Effect>();
        @lock.EnterReadLock();
        try
        {
            foreach (Effect effect in effectMap.Values)
            {
                // check count
                if (count == 0)
                    break;
                if (effectType != null)
                {
                    if (!effect.GetSkillTemplate().HasAnyEffect(effectType.Value))
                        continue;
                    if (effectType == EffectType.STUN && effect.GetSkillId() == 11904)
                    { // avatar skill irremovable by Remove Shock TODO: logic
                        continue;
                    }
                }
                if (dispelSlotType != null)
                {
                    if (effect.GetTargetSlot() != SkillTargetSlotExtensions.Of(dispelSlotType.Value))
                        continue;
                }
                // check dispel level
                if (effect.GetReqDispelLevel() > dispelLevel)
                    continue;

                if (RemovePower(effect, power))
                    effectsToEnd.Add(effect);
                // decrease count
                count--;
            }
        }
        finally
        {
            @lock.ExitReadLock();
        }
        foreach (Effect effect in effectsToEnd)
            effect.EndEffect();
        return count;
    }

    /// <summary>
    /// Calculates effects that will be removed and reduces their power. Used only in DispelBuffCounterAtkEffect.
    /// Returns number of effects that will be removed.
    /// </summary>
    public int CalculateBuffsOrEffectorDebuffsToRemove(Effect effect, int count, int dispelLevel, int power)
    {
        int dispelledEffectCount = 0;
        @lock.EnterReadLock();
        try
        {
            foreach (Effect ef in abnormalEffectMap.Values)
            {
                if (count == 0)
                    break;
                if (!IsDispellable(ef))
                    continue;
                DispelCategoryType dispelCat = ef.GetDispelCategory();
                SkillTargetSlot targetSlot = ef.GetSkillTemplate().GetTargetSlot();
                // check for targetslot, effects with target slot higher or equal to 2 cant be removed (ex. skillId: 11885)
                if (targetSlot != SkillTargetSlot.BUFF && (targetSlot != SkillTargetSlot.DEBUFF && dispelCat != DispelCategoryType.ALL)
                    || ef.GetTargetSlotLevel() >= 2)
                    continue;

                // remove only debuffs of the effector
                if (targetSlot == SkillTargetSlot.DEBUFF && !effect.GetEffector().Equals(ef.GetEffector()))
                    continue;

                switch (dispelCat)
                {
                    case DispelCategoryType.ALL:
                    case DispelCategoryType.BUFF:// DispelBuffCounterAtkEffect
                        if (ef.GetReqDispelLevel() <= dispelLevel)
                        {
                            if (RemovePower(ef, power))
                            {
                                ef.SetDesignatedDispelEffect(effect);
                                dispelledEffectCount++;
                            }
                            count--;
                        }
                        break;
                }
            }
        }
        finally
        {
            @lock.ExitReadLock();
        }
        return dispelledEffectCount;
    }

    public void RemoveEffectByDispelCat(DispelCategoryType dispelCat, SkillTargetSlot targetSlot, int count, int dispelLevel, int power)
    {
        List<Effect> effectsToEnd = new List<Effect>();
        bool insufficientDispelPower = false;
        bool insufficientDispelLevel = false;
        @lock.EnterReadLock();
        try
        {
            foreach (Effect effect in abnormalEffectMap.Values)
            {
                if (count == 0)
                    break;
                if (!IsDispellable(effect))
                    continue;
                // check for targetslot, effects with target slot level higher or equal to 2 cant be removed (ex. skillId: 11885)
                if (effect.GetTargetSlot() != targetSlot || effect.GetTargetSlotLevel() >= 2)
                    continue;

                bool remove = false;
                switch (dispelCat)
                {
                    case DispelCategoryType.ALL:// DispelDebuffEffect
                        if ((effect.GetDispelCategory() == DispelCategoryType.ALL || effect.GetDispelCategory() == DispelCategoryType.DEBUFF_MENTAL
                            || effect.GetDispelCategory() == DispelCategoryType.DEBUFF_PHYSICAL) && effect.GetReqDispelLevel() <= dispelLevel)
                            remove = true;
                        break;
                    case DispelCategoryType.DEBUFF_MENTAL:// DispelDebuffMentalEffect
                        if ((effect.GetDispelCategory() == DispelCategoryType.ALL || effect.GetDispelCategory() == DispelCategoryType.DEBUFF_MENTAL)
                            && effect.GetReqDispelLevel() <= dispelLevel)
                            remove = true;
                        break;
                    case DispelCategoryType.DEBUFF_PHYSICAL:// DispelDebuffPhysicalEffect
                        if ((effect.GetDispelCategory() == DispelCategoryType.ALL || effect.GetDispelCategory() == DispelCategoryType.DEBUFF_PHYSICAL)
                            && effect.GetReqDispelLevel() <= dispelLevel)
                            remove = true;
                        break;
                    case DispelCategoryType.BUFF:// DispelBuffEffect or DispelBuffCounterAtkEffect
                        if (effect.GetDispelCategory() == DispelCategoryType.BUFF && effect.GetReqDispelLevel() <= dispelLevel)
                            remove = true;
                        break;
                    case DispelCategoryType.STUN:
                        if (effect.GetDispelCategory() == DispelCategoryType.STUN)
                            remove = true;
                        break;
                    case DispelCategoryType.NPC_BUFF:// DispelNpcBuff
                        if (effect.GetDispelCategory() == DispelCategoryType.NPC_BUFF)
                            remove = true;
                        break;
                    case DispelCategoryType.NPC_DEBUFF_PHYSICAL:// DispelNpcDebuff
                        if (effect.GetDispelCategory() == DispelCategoryType.NPC_DEBUFF_PHYSICAL)
                            remove = true;
                        break;
                }

                if (remove)
                {
                    if (RemovePower(effect, power))
                    {
                        effectsToEnd.Add(effect);
                    }
                    else if (owner is Player)
                    {
                        insufficientDispelPower = true;
                    }
                    count--;
                }
                else
                    insufficientDispelLevel = true;
            }
        }
        finally
        {
            @lock.ExitReadLock();
        }
        foreach (Effect effect in effectsToEnd) // end outside lock so broadcasting effects can't cause deadlocks
            effect.EndEffect();
        if (owner is Player)
        {
            if (insufficientDispelPower)
                PacketSendUtility.SendPacket((Player)owner, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_DISPELCOUNT());
            if (insufficientDispelLevel)
                PacketSendUtility.SendPacket((Player)owner, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_DISPELLEVEL());
        }
    }

    private bool IsNoShowToggle(Effect effect)
    {
        return effect.GetTargetSlot() == SkillTargetSlot.NOSHOW && effect.IsToggle();
    }

    private bool IsDispellable(Effect effect)
    {
        if (IsNoShowToggle(effect))
            return false;
        if (effect.IsSanctuaryEffect())
            return false;
        // skip effects that are about to be removed by another dispel effect
        if (effect.GetDesignatedDispelEffect() != null)
            return false;
        // effects with duration 86400000 cant be dispelled
        // TODO recheck
        if (effect.GetDuration() >= 86400000 && !IsRemovableEffect(effect))
            return false;
        return true;
    }

    public void DispelBuffCounterAtkEffect(Effect effect)
    {
        List<Effect> effectsToEnd = FilterEffects(abnormalEffectMap, e => effect.Equals(e.GetDesignatedDispelEffect()));
        foreach (Effect ef in effectsToEnd)
        {
            ef.EndEffect();
        }
    }

    // TODO this should be removed
    private bool IsRemovableEffect(Effect effect)
    {
        int skillId = effect.GetSkillId();
        switch (skillId)
        {
            case 20941:
            case 20942:
            case 19370:
            case 19371:
            case 19372:
            case 20530:
            case 20531:
            case 19345:
            case 19346:
            case 21438:
            case 21121:
            case 21124:
                // TODO
                return true;
            default:
                return false;
        }
    }

    private bool RemovePower(Effect effect, int power)
    {
        return effect.RemovePower(power) <= 0;
    }

    /// <summary>
    /// Removes all effects from controllers and ends them appropriately. Passive effects will not be removed.
    /// </summary>
    public virtual void RemoveAllEffects()
    {
        RemoveAllEffects(false);
    }

    public virtual void RemoveAllEffects(bool logout)
    {
        List<Effect> effects;
        if (logout)
        { // remove all effects on logout
            effects = GetAllEffects();
        }
        else
        {
            effects = FilterEffects(abnormalEffectMap, CanRemoveOnDie);
        }
        foreach (Effect effect in effects) // end outside lock so broadcasting effects can't cause deadlocks
            effect.EndEffect(false);
        if (!logout)
            BroadCastEffects(null);
    }

    protected virtual bool CanRemoveOnDie(Effect effect)
    {
        return effect.CanRemoveOnDie();
    }

    public bool IsUnderFear()
    {
        return IsAbnormalSet(AbnormalState.FEAR);
    }

    public bool IsConfused()
    {
        return IsAbnormalSet(AbnormalState.CONFUSE);
    }

    /// <summary>Returns a copy of the abnormals list.</summary>
    public List<Effect> GetAbnormalEffects()
    {
        return FilterEffects(abnormalEffectMap, e => !IsNoShowToggle(e));
    }

    /// <summary>Returns the list of effects to display as top icons.</summary>
    public List<Effect> GetAbnormalEffectsToTargetSlot(int slot)
    {
        return FilterEffects(abnormalEffectMap, e => !IsNoShowToggle(e) && e.GetTargetSlot().GetId() == slot);
    }

    /// <summary>Returns the list of effects to display as top icons.</summary>
    public List<Effect> GetAbnormalEffectsToShow()
    {
        return FilterEffects(abnormalEffectMap, e => e.GetTargetSlot() != SkillTargetSlot.NOSHOW);
    }

    public void SetAbnormal(AbnormalState state)
    {
        owner.GetObserveController().NotifyAbnormalSettedObservers(state);
        abnormals |= state.GetId();

        // TODO move to observer?
        // if player is sitting when setting certain abnormal states, he is forcefully made to stand up
        if (owner is Player && owner.IsInState(CreatureState.Resting))
        {
            if (IsInAnyAbnormalState(AbnormalState.AUTOMATICALLY_STANDUP))
            {
                owner.UnsetState(CreatureState.Resting);
                PacketSendUtility.BroadcastPacket((Player)owner, new SM_EMOTION(owner, EmotionType.Stand), true);
            }
        }
    }

    public void UnsetAbnormal(AbnormalState state)
    {
        int count = 0;
        @lock.EnterReadLock();
        try
        {
            foreach (Effect effect in abnormalEffectMap.Values)
            {
                if ((effect.GetAbnormals() & state.GetId()) == state.GetId())
                {
                    if (++count == 2)
                        return;
                }
            }
        }
        finally
        {
            @lock.ExitReadLock();
        }
        abnormals &= ~state.GetId();
    }

    /// <summary>Used for checking unique abnormal states.</summary>
    public bool IsAbnormalSet(AbnormalState state)
    {
        if (state == AbnormalState.NONE)
            return abnormals == 0;
        return (abnormals & state.GetId()) == state.GetId();
    }

    /// <summary>Used for compound abnormal state checks.</summary>
    public bool IsInAnyAbnormalState(AbnormalState state)
    {
        if (state == AbnormalState.NONE)
            return abnormals == 0;
        return (abnormals & state.GetId()) != 0;
    }

    public int GetAbnormals()
    {
        return abnormals;
    }

    public bool IsEmpty()
    {
        return abnormalEffectMap.Count == 0;
    }

    public void ResetDesignatedDispelEffect(Effect effect)
    {
        @lock.EnterReadLock();
        try
        {
            foreach (Effect ef in abnormalEffectMap.Values)
            {
                if (effect.Equals(ef.GetDesignatedDispelEffect()))
                {
                    ef.ResetDesignatedDispelEffect();
                }
            }
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }
}
