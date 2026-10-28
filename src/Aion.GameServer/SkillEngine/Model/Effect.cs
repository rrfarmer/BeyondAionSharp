using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Network.Aion.ServerPackets;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.PeriodicAction;
using Aion.GameServer.Utils;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Java parity: skillengine/model/Effect (@author ATracer, Wakizashi, Sippolo, kecimis, Neon).
/// Runtime instance of a skill's effect lifecycle: INITIALIZE - APPLY - START - END.
/// </summary>
public class Effect : IStatOwner
{
    private readonly Creature effector;
    private readonly Creature effected;
    private readonly SkillTemplate skillTemplate;
    private Skill skill;
    private int skillLevel;
    private int? duration;
    private long endTime;
    private SubEffectType subEffectType = SubEffectType.NONE;
    private ScheduledTask endTask = null;
    private ScheduledTask[] periodicTasks = null;
    private ScheduledTask periodicActionsTask = null;

    private int effectedHp = -1;

    private readonly HashSet<EffectReserved> reservedEffects = new HashSet<EffectReserved>();

    private SpellStatus spellStatus = SpellStatus.NONE;
    private DashStatus dashStatus = DashStatus.NONE;
    private AttackStatus attackStatus = AttackStatus.NORMALHIT;
    private readonly bool[] magicalCriticals = new bool[4];
    private bool magicalCriticalRolled;
    private bool magicalCritical;

    /// <summary>shield effects related</summary>
    private int shieldDefense;
    private int reflectedDamage = 0;
    private int reflectedSkillId = 0;
    private int protectedSkillId = 0;
    private int protectedDamage = 0;
    private int protectorId = 0;
    private int mpAbsorbed = 0;
    private int mpShieldSkillId = 0;

    private bool addedToController;
    private readonly List<System.Action> observerRemoveTasks = new List<System.Action>();
    private bool launchSubEffect = true;
    private Effect subEffect;

    private readonly AtomicBoolean hasEnded = new AtomicBoolean();
    private int preparedNpcDrainInitialAborted;
    private bool isCancelOnDmg;
    private bool subEffectAbortedBySubConditions;

    /// <summary>Hate that will be placed on effected list</summary>
    private int tauntHate;
    /// <summary>Total hate that will be broadcasted</summary>
    private int effectHate;

    private readonly ConcurrentDictionary<int, EffectTemplate> successEffects = new ConcurrentDictionary<int, EffectTemplate>();

    private int carvedSignet = 0;

    private int signetBurstedCount = 0;

    private int abnormals;

    private float targetX, targetY, targetZ;
    private float x, y, z;
    private int worldId, instanceId;

    private ForceType forceType;

    /// <summary>power of effect ( used for dispels)</summary>
    private int power;

    private EffectResult effectResult = EffectResult.NORMAL;

    private bool endedByTime = false;

    private Effect designatedDispelEffect = null;

    // Whether this effect is a sub effect of another effect
    private bool isSubEffect = false;
    private bool applyCriticalProcEffect = false;
    // Set only after the NPC statistics adapter's source/host/owner preflights.
    // The shared4.8 skill template retains its original protocol/content data.
    private bool retailNpcPhysicalPrimary;
    private readonly AtomicBoolean allowGodstoneActivation = new AtomicBoolean();

    public Effect(Skill skill, Creature effected)
        : this(skill.GetEffector(), effected, skill.GetSkillTemplate(), skill.GetSkillLevel(), null, null)
    {
        this.effectHate = skill.GetHate();
        this.skill = skill;
        ActivationAttribute activationAttribute = skillTemplate.GetActivationAttribute();
        if (activationAttribute == ActivationAttribute.ACTIVE || activationAttribute == ActivationAttribute.CHARGE)
            this.allowGodstoneActivation.Set(true);
    }

    public Effect(Creature effector, Creature effected, SkillTemplate skillTemplate, int skillLevel)
        : this(effector, effected, skillTemplate, skillLevel, null, null)
    {
    }

    /// <summary>
    /// If duration is null, it will be calculated upon execution, else the forced value will be used.
    /// </summary>
    public Effect(Creature effector, Creature effected, SkillTemplate skillTemplate, int skillLevel, int? duration, ForceType forceType)
        : this(effector, effected, skillTemplate, skillLevel, duration, forceType, false, null)
    {
    }

    public Effect(Creature effector, Creature effected, SkillTemplate skillTemplate, int skillLevel, int? duration, ForceType forceType, bool isSubEffect, ISet<int>? magicalCriticalPositions)
    {
        this.effector = effector;
        this.effected = effected;
        this.skillTemplate = skillTemplate;
        this.skillLevel = skillLevel;
        this.duration = duration;
        this.forceType = forceType;
        this.isSubEffect = isSubEffect;
        this.power = skillTemplate.GetReqDispelCount();
        if (magicalCriticalPositions != null)
            SetMagicalCriticals(magicalCriticalPositions);
    }

    public void SetWorldPosition(int worldId, int instanceId, float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.worldId = worldId;
        this.instanceId = instanceId;
    }

    public int GetEffectorId()
    {
        return effector.GetObjectId();
    }

    public Skill GetSkill()
    {
        return skill;
    }

    public void SetAbnormal(AbnormalState state)
    {
        abnormals |= state.GetId();
    }

    public int GetAbnormals()
    {
        return abnormals;
    }

    public int GetSkillId()
    {
        return skillTemplate.GetSkillId();
    }

    public string GetSkillName()
    {
        return skillTemplate.GetName();
    }

    public SkillTemplate GetSkillTemplate()
    {
        return skillTemplate;
    }

    public SkillSubType GetSkillSubType()
    {
        return skillTemplate.GetSubType();
    }

    public string GetStack()
    {
        return skillTemplate.GetStack();
    }

    public int GetSkillLevel()
    {
        return skillLevel;
    }

    public int GetSkillStackLvl()
    {
        return skillTemplate.GetLvl();
    }

    public SkillType GetSkillType()
    {
        return skillTemplate.GetTypeValue();
    }

    public int GetDuration()
    {
        return duration == null ? 0 : duration.Value;
    }

    /// <summary>
    /// The effected from effect constructor, can differ from getEffected if effect got reflected via shield observer.
    /// </summary>
    public Creature GetOriginalEffected()
    {
        return effected;
    }

    /// <summary>
    /// The creature which receives the damage/skill results. It will be the effector if <see cref="IsReflected"/> returns true.
    /// </summary>
    public Creature GetEffected()
    {
        return IsReflected() ? effector : effected;
    }

    public Creature GetEffector()
    {
        return effector;
    }

    public bool IsPassive()
    {
        return skillTemplate.IsPassive();
    }

    public bool IsPeriodic()
    {
        return periodicTasks != null;
    }

    public void SetPeriodicTask(ScheduledTask periodicTask, int position)
    {
        if (periodicTasks == null)
            periodicTasks = new ScheduledTask[4];
        else if (periodicTasks[position - 1] != null && periodicTask != null)
        {
            periodicTask.Cancel();
            throw new InvalidOperationException(GetType().Name + " already has a periodic task at position " + position);
        }
        this.periodicTasks[position - 1] = periodicTask;
    }

    public AttackStatus GetAttackStatus()
    {
        return attackStatus;
    }

    public void SetAttackStatus(AttackStatus attackStatus)
    {
        this.attackStatus = attackStatus;
    }

    public bool IsMagicalCritical(int position)
    {
        return magicalCriticals[position - 1];
    }

    /// <summary>
    /// Rolls the magical critical for the given effect position, or takes over the one an earlier position already rolled, since a cast only rolls once
    /// per target.
    /// </summary>
    public void RollMagicalCritical(int position, int criticalProb)
    {
        if (!magicalCriticalRolled)
        {
            magicalCritical = StatFunctions.CalculateMagicalCriticalRate(effector, effected, criticalProb);
            magicalCriticalRolled = true;
        }
        ReuseMagicalCritical(position);
    }

    /// <summary>Takes over the magical critical of an earlier effect position without rolling one.</summary>
    public void ReuseMagicalCritical(int position)
    {
        magicalCriticals[position - 1] = magicalCritical;
    }

    /// <summary>Import a decision calculated by the scoped retail NPC adapter. This owns
    /// the same cache and wire/periodic position bits as RollMagicalCritical, without a second roll.</summary>
    internal void SetCalculatedMagicalCritical(int position, bool critical)
    {
        if (position < 1 || position > magicalCriticals.Length)
            throw new ArgumentOutOfRangeException(nameof(position));
        magicalCritical = critical;
        magicalCriticalRolled = true;
        magicalCriticals[position - 1] = critical;
    }

    internal bool TryGetCalculatedMagicalCritical(out bool critical)
    {
        critical=magicalCritical;
        return magicalCriticalRolled;
    }

    /// <summary>An effect which got filtered out breaks the chain, so the next effect position rolls its own magical critical again.</summary>
    public void ResetMagicalCritical()
    {
        magicalCritical = false;
        magicalCriticalRolled = false;
    }

    private void SetMagicalCriticals(ISet<int> positions)
    {
        magicalCritical = false;
        magicalCriticalRolled = true;
        for (int i = 0; i < magicalCriticals.Length; i++)
        {
            int position = i + 1;
            magicalCriticals[i] = positions.Contains(position);
            magicalCritical |= magicalCriticals[i];
        }
    }

    public List<EffectTemplate> GetEffectTemplates()
    {
        return skillTemplate.GetEffects().GetEffects();
    }

    public bool IsToggle()
    {
        return skillTemplate.GetActivationAttribute() == ActivationAttribute.TOGGLE;
    }

    public bool IsChant()
    {
        return skillTemplate.GetTargetSlot() == SkillTargetSlot.CHANT;
    }

    public SkillTargetSlot GetTargetSlot()
    {
        return skillTemplate.GetTargetSlot();
    }

    public int GetTargetSlotLevel()
    {
        return skillTemplate.GetTargetSlotLevel();
    }

    public DispelCategoryType GetDispelCategory()
    {
        return skillTemplate.GetDispelCategory();
    }

    public int GetReqDispelLevel()
    {
        return skillTemplate.GetReqDispelLevel();
    }

    public int GetEffectedHp()
    {
        return effectedHp;
    }

    public EffectReserved GetReserveds(int position)
    {
        lock (reservedEffects)
        {
            foreach (EffectReserved er in reservedEffects)
            {
                if (er.GetPosition() == position)
                    return er;
            }
        }
        return new EffectReserved(0, 0, EffectReserved.ResourceType.HP, true, false);
    }

    public void SetReserveds(EffectReserved er, bool overTimeEffect)
    {
        // set effected hp
        // TODO RI_ChargeAttack_G, RI_ChargingFlight_G
        bool instantSkill = false;
        if (this.GetSkill() != null && this.GetSkill().IsInstantSkill())
            instantSkill = true;
        if (er.GetType_() == EffectReserved.ResourceType.HP && er.GetValue() != 0 && !overTimeEffect && !instantSkill && !GetEffected().IsInvulnerable())
        {
            Creature effected = GetEffected();
            int value = (er.IsDamage() ? -er.GetValue() : er.GetValue());
            value += effected.GetLifeStats().GetCurrentHp();
            if (value <= 0)
            {
                value = 0;
                // effected is about to die
                if (!effected.IsDead())
                    effected.GetLifeStats().SetKillingBlow(er.GetValue());
                effectedHp = 0;
            }
            else
            {
                effectedHp = Math.Max(1, (int)(100f * value / effected.GetLifeStats().GetMaxHp()));
            }
        }
        lock (reservedEffects)
        {
            reservedEffects.Add(er);
        }
    }

    // SetReserveds deliberately keeps Java's append semantics. A selected
    // statistics heal can retire after Check; replace only its exact owned
    // packet slot so legacy Apply cannot read an earlier selected reservation.
    internal void ReplaceStatisticsHealReservation(EffectReserved selected,EffectReserved replacement)
    {
        if(!Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatHealCombat.OwnsMetadata(this,selected)
            ||selected.GetType_()!=EffectReserved.ResourceType.HP||selected.IsDamage()
            ||replacement.GetType_()!=EffectReserved.ResourceType.HP||replacement.IsDamage()
            ||selected.GetPosition()!=replacement.GetPosition())
            throw new InvalidOperationException("Statistics heal does not own the replacement reservation");
        lock(reservedEffects)
        {
            if(!reservedEffects.Remove(selected))throw new InvalidOperationException("Selected statistics heal reservation disappeared");
            if(!reservedEffects.Add(replacement))throw new InvalidOperationException("Replacement statistics heal reservation already exists");
        }
    }

    public ISet<EffectReserved> GetReservedEffectsToSend()
    {
        SortedSet<EffectReserved> toSend = new SortedSet<EffectReserved>();
        lock (reservedEffects)
        {
            foreach (EffectReserved er in reservedEffects)
            {
                if (er.IsSend() && er.GetValue() != 0)
                    toSend.Add(er);
            }
        }
        if (toSend.Count == 0) // effects without a sent value (like damage over time) can still show their attack status
            return new HashSet<EffectReserved> { new EffectReserved(0, 0, EffectReserved.ResourceType.HP, true, true, attackStatus) };
        return toSend;
    }

    public bool IsLaunchSubEffect()
    {
        return launchSubEffect;
    }

    public void SetLaunchSubEffect(bool launchSubEffect)
    {
        this.launchSubEffect = launchSubEffect;
    }

    public int GetShieldDefense()
    {
        return shieldDefense;
    }

    public void SetShieldDefense(int shieldDefense)
    {
        if ((shieldDefense & ShieldType.SKILL_REFLECTOR.GetId()) != 0 && GetSkillSubType() != SkillSubType.ATTACK && GetSkillSubType() != SkillSubType.DEBUFF)
            shieldDefense &= ~ShieldType.SKILL_REFLECTOR.GetId(); // disable SKILL_REFLECTOR bit (only attack type effects can reflect whole effects)
        this.shieldDefense = shieldDefense;
    }

    /// <summary>True if the whole effect is reflected (through shield observer)</summary>
    public bool IsReflected()
    {
        return (shieldDefense & ShieldType.SKILL_REFLECTOR.GetId()) != 0;
    }

    public int GetReflectedDamage()
    {
        return this.reflectedDamage;
    }

    public void SetReflectedDamage(int value)
    {
        this.reflectedDamage = value;
    }

    public int GetReflectedSkillId()
    {
        return this.reflectedSkillId;
    }

    public void SetReflectedSkillId(int value)
    {
        this.reflectedSkillId = value;
    }

    public int GetProtectedSkillId()
    {
        return this.protectedSkillId;
    }

    public void SetProtectedSkillId(int skillId)
    {
        this.protectedSkillId = skillId;
    }

    public int GetProtectedDamage()
    {
        return this.protectedDamage;
    }

    public void SetProtectedDamage(int protectedDamage)
    {
        this.protectedDamage = protectedDamage;
    }

    public int GetProtectorId()
    {
        return this.protectorId;
    }

    public void SetProtectorId(int protectorId)
    {
        this.protectorId = protectorId;
    }

    public SpellStatus GetSpellStatus()
    {
        return spellStatus;
    }

    public void SetSpellStatus(SpellStatus spellStatus)
    {
        this.spellStatus = spellStatus;
    }

    public DashStatus GetDashStatus()
    {
        return dashStatus;
    }

    public void SetDashStatus(DashStatus dashStatus)
    {
        this.dashStatus = dashStatus;
    }

    /// <summary>Number of signets carved on target</summary>
    public int GetCarvedSignet()
    {
        return this.carvedSignet;
    }

    public void SetCarvedSignet(int value)
    {
        this.carvedSignet = value;
    }

    public Effect GetSubEffect()
    {
        return subEffect;
    }

    public void SetSubEffect(Effect subEffect)
    {
        this.subEffect = subEffect;
    }

    public bool ContainsEffectId(int effectId)
    {
        foreach (EffectTemplate template in successEffects.Values)
        {
            if (template.GetEffectId() == effectId)
                return true;
        }
        return false;
    }

    public void SetForceType(ForceType forceType)
    {
        this.forceType = forceType;
    }

    public ForceType GetForceType()
    {
        return forceType;
    }

    public bool IsForcedEffect()
    {
        return forceType != null;
    }

    internal void MarkRetailNpcPhysicalPrimary()=>retailNpcPhysicalPrimary=true;

    public bool IsPhysicalEffect()
    {
        if(retailNpcPhysicalPrimary)return true;
        EffectTemplate mainEffectTemplate = skillTemplate.GetEffectTemplate(1);
        return mainEffectTemplate != null && mainEffectTemplate.GetElement() == SkillElement.NONE;
    }

    /// <summary>
    /// Do initialization with proper calculations.<br/>
    /// Correct lifecycle of Effect: INITIALIZE - APPLY - START - END
    /// </summary>
    public void Initialize()
    {
        if (skillTemplate.GetEffects() == null)
            return;

        if (effected != null && effected.GetEffectController().IsConflicting(this))
            SetEffectResult(EffectResult.CONFLICT);
        if (effectResult != EffectResult.CONFLICT)
        {
            foreach (EffectTemplate template in GetEffectTemplates())
            {
                template.Calculate(this);
            }
        }
        if (!IsInSuccessEffects(1))
        {
            successEffects.Clear();
        }
        else
        {
            if (effectHate == 0) // can be overridden from constructor with skill (from pet order)
                effectHate = CalculateHateForSuccessEffects();
            if (IsLaunchSubEffect())
            {
                foreach (EffectTemplate template in successEffects.Values)
                {
                    template.CalculateSubEffect(this);
                }
            }
            if (effector is Player p && GetAttackStatus() == AttackStatus.CRITICAL && GetSubEffect() == null && !IsPeriodic() && Rnd.Chance() < 10)
            {
                Effect criticalProcEffect = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().CreateCriticalProcEffect(p, GetEffected(), skillTemplate.GetSkillId());
                if (criticalProcEffect != null && criticalProcEffect.GetEffectResult() != EffectResult.DODGE && criticalProcEffect.GetEffectResult() != EffectResult.RESIST)
                {
                    applyCriticalProcEffect = true;
                    SetSpellStatus(criticalProcEffect.GetSpellStatus());
                    SetSubEffect(criticalProcEffect);
                    SetSubEffectType(criticalProcEffect.GetSubEffectType());
                    SetTargetLoc(criticalProcEffect.GetTargetX(), criticalProcEffect.GetTargetY(), criticalProcEffect.GetTargetZ());
                }
            }
        }

        if (successEffects.IsEmpty)
        {
            if (effectResult == EffectResult.CONFLICT)
            {
                SetAttackStatus(AttackStatus.DODGE);
            }
            else if (IsPhysicalEffect())
            {
                if (GetAttackStatus() == AttackStatus.CRITICAL)
                    SetAttackStatus(AttackStatus.CRITICAL_DODGE);
                else
                    SetAttackStatus(AttackStatus.DODGE);
                SetSpellStatus(SpellStatus.DODGE2);
                effectResult = EffectResult.DODGE;
            }
            else
            {
                if (GetAttackStatus() == AttackStatus.CRITICAL)
                    SetAttackStatus(AttackStatus.CRITICAL_RESIST);// TODO recheck
                else
                    SetAttackStatus(AttackStatus.RESIST);
                SetSpellStatus(SpellStatus.NONE);
                effectResult = EffectResult.RESIST;
            }
        }

        // set spellstatus for sm_castspell_end packet
        switch (AttackStatusExtensions.GetBaseStatus(GetAttackStatus()))
        {
            case AttackStatus.DODGE:
                if (effectResult != EffectResult.CONFLICT)
                    SetSpellStatus(SpellStatus.DODGE);
                break;
            case AttackStatus.PARRY:
                if (GetSpellStatus() == SpellStatus.NONE)
                    SetSpellStatus(SpellStatus.PARRY);
                break;
            case AttackStatus.BLOCK:
                if (GetSpellStatus() == SpellStatus.NONE)
                    SetSpellStatus(SpellStatus.BLOCK);
                break;
            case AttackStatus.RESIST:
                SetSpellStatus(SpellStatus.RESIST);
                break;
        }
    }

    private int CalculateHateForSuccessEffects()
    {
        int effectHate = 0;
        foreach (EffectTemplate template in successEffects.Values)
        {
            effectHate += template.CalculateHate(this);
        }
        return effectHate == 0 ? 0 : StatFunctions.CalculateHate(GetEffector(), effectHate);
    }

    /// <summary>Apply all effect templates</summary>
    public void ApplyEffect()
    {
        if (successEffects.IsEmpty)
        {
            BroadcastHate();
            return;
        }

        Creature effected = GetEffected();
        try
        {
            // The guarded drain primary must finish its HP decision before a
            // dependent effect can register. ConcurrentDictionary does not
            // promise position order; leave other skill families unchanged.
            IEnumerable<EffectTemplate> applying = RetailNpcStatSpellDrain.HasGuardedInitial(this)
                ? successEffects.OrderBy(entry => entry.Key).Select(entry => entry.Value)
                : successEffects.Values;
            foreach (EffectTemplate template in applying)
            {
                if (!ShouldApplyFurtherEffects(effected))
                    break;
                template.ApplyEffect(this);
                if (!ShouldApplyFurtherEffects(effected))
                    break;
                if (RetailNpcStatSpellDrain.InitialCommitPending(this))
                {
                    if (RetailNpcStatSpellDrain.HasDeferredVampireTimeline(this)) continue;
                    break;
                }
                template.StartSubEffect(this);
            }
            if (!PreparedNpcDrainInitialAborted && !RetailNpcStatSpellDrain.InitialCommitPending(this) && applyCriticalProcEffect && subEffect != null)
                subEffect.ApplyEffect();
            if (!PreparedNpcDrainInitialAborted && !RetailNpcStatSpellDrain.InitialCommitPending(this) && effected != null)
                effected.GetAi().OnEffectApplied(this);
        }
        catch (Exception e)
        {
            throw new Exception("Error applying effect of skill " + GetSkillId() + " from " + effector + " to " + effected, e);
        }
    }

    public void BroadcastHate()
    {
        if (effectHate != 0 && tauntHate >= 0)
        { // don't add hate if taunt hate is < 0!
            Creature effected = GetEffected();
            effected.GetAggroList().AddHate(effector, effectHate);
            if (skillTemplate.GetHostileType() == HostileType.INDIRECT)
            {
                effected.GetKnownList().ForEachObject(visibleObject =>
                {
                    if (visibleObject is Creature)
                    {
                        AggroList al = ((Creature)visibleObject).GetAggroList();
                        if (al.IsHating(effector) || al.IsHating(effected))
                            al.AddHate(effector, effectHate);
                    }
                });
            }
            effectHate = 0; // set to 0 to avoid external second broadcast
        }
    }

    private bool ShouldApplyFurtherEffects(Creature effected)
    {
        if (PreparedNpcDrainInitialAborted) return false;
        if (effected != null)
        {
            if (!effected.IsSpawned() && !skillTemplate.IsPassive()) // only allow on despawned if it's a passive skill (players get them during enterWorld)
                return false;
            if (effected.IsDead() && !skillTemplate.HasResurrectEffect())
                return false;
        }
        return true;
    }

    internal bool PreparedNpcDrainInitialAborted => Volatile.Read(ref preparedNpcDrainInitialAborted)!=0;
    internal void AbortPreparedNpcDrainInitial() => Interlocked.Exchange(ref preparedNpcDrainInitialAborted,1);

    /// <summary>
    /// Start effect which includes: - start effect defined in template - start subeffect if possible - activate toggle skill if needed - schedule end of effect
    /// </summary>
    public void StartEffect()
    {
        lock (this)
        {
            if (hasEnded.Get() || PreparedNpcDrainInitialAborted || RetailNpcStatSpellDrain.InitialCommitPending(this))
                return;
            if (successEffects.IsEmpty)
                return;

            if (duration == null)
            {
                if (IsToggle())
                {
                    if (effector is Player)
                        ActivateToggleSkill();
                    duration = skillTemplate.GetToggleTimer();
                }
                else
                {
                    duration = CalculateEffectsDuration();
                }
            }

            SchedulePeriodicActions();

            if (!successEffects.IsEmpty)
            {
                foreach (EffectTemplate template in successEffects.Values)
                    template.StartEffect(this);
                AddCancelOnDmgObserver();
            }

            BroadcastHate();

            if (duration == 0)
                return;
            endTime = SystemClock.CurrentMillis() + duration.Value;

            if (!SkyTempleNpcPeriodicEffects.Owns(this))
            {
                endTask = ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    endedByTime = true;
                    EndEffect(true);
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(duration.Value));
            }
        }
        effected.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnStartEffect(this);
    }

    /// <summary>Will visually activate toggle skill</summary>
    private void ActivateToggleSkill()
    {
        PacketSendUtility.SendPacket((Player)effector, new SM_SKILL_ACTIVATION(GetSkillId(), true));
    }

    /// <summary>Will visually deactivate toggle skill</summary>
    private void DeactivateToggleSkill()
    {
        PacketSendUtility.SendPacket((Player)effector, new SM_SKILL_ACTIVATION(GetSkillId(), false));
    }

    public void EndEffect()
    {
        EndEffect(true);
    }

    /// <summary>End effect and all effect actions</summary>
    public void EndEffect(bool broadcast)
    {
        lock (this)
        { // wait for startEffect before ending
            if (!hasEnded.CompareAndSet(false, true))
                return;
        }
        SkyTempleNpcPeriodicEffects.Retire(this);
        StopTasks();
        RemoveObservers();

        EndEffects();

        // if effect is a stance, remove stance from player
        if (effector is Player player)
        {
            if (player.GetController().GetStanceSkillId() == GetSkillId())
                player.GetController().StopStance();
        }

        Creature effected = GetEffected();
        // TODO better way to finish
        if (GetSkillTemplate().GetTargetSlot() == SkillTargetSlot.SPEC2)
        {
            effected.GetLifeStats().IncreaseHp(SmAttackStatus.TYPE.REGULAR, (int)(effected.GetLifeStats().GetMaxHp() * 0.2f), GetEffector());
            effected.GetLifeStats().IncreaseMp((int)(effected.GetLifeStats().GetMaxMp() * 0.2f));
        }

        if (IsToggle() && effector is Player)
        {
            DeactivateToggleSkill();
        }
        effected.GetEffectController().ClearEffect(this, broadcast);

        effected.GetAi().OnEffectEnd(this);
        effected.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnEndEffect(this);
    }

    /// <summary>Stop all scheduled tasks</summary>
    public void StopTasks()
    {
        if (endTask != null)
        {
            endTask.Cancel();
            endTask = null;
        }

        if (periodicTasks != null)
        {
            foreach (ScheduledTask periodicTask in periodicTasks)
            {
                if (periodicTask != null)
                    periodicTask.Cancel();
            }
            periodicTasks = null;
        }

        StopPeriodicActions();
    }

    /// <summary>Time in milliseconds till the effect ends</summary>
    public long GetRemainingTimeMillis()
    {
        return endTime - SystemClock.CurrentMillis();
    }

    public int GetRemainingTimeToDisplay()
    {
        if (GetDuration() == 0) // permanent effect (or not yet started, should not happen)
            return -1;
        if (duration >= 86400000 && effected is Npc) // >= 24h
            return -1;
        long remainingTimeMillis = GetRemainingTimeMillis();
        return remainingTimeMillis > int.MaxValue ? -1 : (int)remainingTimeMillis;
    }

    public bool CanSaveOnLogout()
    {
        if (skillTemplate.IsNoSaveOnLogout())
            return false;
        if (GetDuration() == 0) // permanent effect, such as toggle or passive skill (or not yet started, should not happen)
            return false;
        if (duration >= 86400000) // effects with duration >= 24h are event or instance related
            return false;
        return true;
    }

    public long GetEndTime()
    {
        return endTime;
    }

    public int GetPvpDamage()
    {
        return skillTemplate.GetPvpDamage();
    }

    public ItemTemplate GetItemTemplate()
    {
        return skill == null ? null : skill.GetItemTemplate();
    }

    /// <summary>Try to add this effect to effected controller</summary>
    public void AddToEffectedController()
    {
        if (PreparedNpcDrainInitialAborted || RetailNpcStatSpellDrain.InitialCommitPending(this)) return;
        if (!addedToController)
        {
            Creature effected = GetEffected();
            if (effected.GetLifeStats() != null && !effected.IsDead())
            {
                effected.GetEffectController().AddEffect(this);
                addedToController = true;
            }
        }
    }

    public int GetEffectHate()
    {
        return effectHate;
    }

    public int GetTauntHate()
    {
        return tauntHate;
    }

    public void SetTauntHate(int tauntHate)
    {
        this.tauntHate = tauntHate;
    }

    public void AddObserver(Creature target, ActionObserver observer)
    {
        target.GetObserveController().AddObserver(observer);
        observerRemoveTasks.Add(() => target.GetObserveController().RemoveObserver(observer));
    }

    public void AddObserver(Creature target, AttackCalcObserver observer)
    {
        target.GetObserveController().AddAttackCalcObserver(observer);
        observerRemoveTasks.Add(() => target.GetObserveController().RemoveAttackCalcObserver(observer));
    }

    private void RemoveObservers()
    {
        foreach (System.Action task in observerRemoveTasks)
            task();
        observerRemoveTasks.Clear();
    }

    public void AddSuccessEffect(EffectTemplate effect)
    {
        successEffects[effect.GetPosition()] = effect;
    }

    public bool IsInSuccessEffects(int position)
    {
        return successEffects.ContainsKey(position);
    }

    public EffectTemplate EffectInPos(int pos)
    {
        return successEffects.TryGetValue(pos, out EffectTemplate value) ? value : null;
    }

    public ICollection<EffectTemplate> GetSuccessEffects()
    {
        return new SuccessEffectsView(successEffects);
    }

    /// <summary>
    /// Java's Map.values(): a view of the success map, not a copy. EffectTemplate.calculate empties the map through it when a later effect
    /// is dodged or resisted (EffectTemplate.java 315-316). ConcurrentDictionary.Values is a read-only snapshot, and Clear on it throws.
    /// </summary>
    private sealed class SuccessEffectsView : ICollection<EffectTemplate>
    {
        private readonly ConcurrentDictionary<int, EffectTemplate> map;

        public SuccessEffectsView(ConcurrentDictionary<int, EffectTemplate> map)
        {
            this.map = map;
        }

        public int Count => map.Count;

        public bool IsReadOnly => false;

        // Java's values() view takes no add.
        public void Add(EffectTemplate item) => throw new NotSupportedException();

        public void Clear() => map.Clear();

        public bool Contains(EffectTemplate item) => map.Values.Contains(item);

        public void CopyTo(EffectTemplate[] array, int arrayIndex) => map.Values.CopyTo(array, arrayIndex);

        public bool Remove(EffectTemplate item)
        {
            foreach (KeyValuePair<int, EffectTemplate> entry in map)
            {
                if (Equals(entry.Value, item))
                    return map.TryRemove(entry.Key, out _);
            }
            return false;
        }

        public IEnumerator<EffectTemplate> GetEnumerator() => map.Values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public void AddAllEffectToSucess()
    {
        successEffects.Clear();
        foreach (EffectTemplate template in GetEffectTemplates())
        {
            successEffects[template.GetPosition()] = template;
        }
    }

    private void SchedulePeriodicActions()
    {
        if (skillTemplate.GetPeriodicActions() == null)
            return;
        PeriodicActions periodicActions = skillTemplate.GetPeriodicActions();
        if (periodicActions.GetPeriodicActions() == null || periodicActions.GetPeriodicActions().Count == 0)
            return;
        int checktime = periodicActions.GetChecktime();
        periodicActionsTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            foreach (Aion.GameServer.SkillEngine.PeriodicAction.PeriodicAction action in periodicActions.GetPeriodicActions())
                action.Act(this);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(checktime), TimeSpan.FromMilliseconds(checktime));
    }

    private void StopPeriodicActions()
    {
        if (periodicActionsTask != null)
        {
            periodicActionsTask.Cancel();
            periodicActionsTask = null;
        }
    }

    private int CalculateEffectsDuration()
    {
        EffectTemplate? durationTemplate = null;
        long duration = 0;
        // The first positive duration wins, even when later effects last longer.
        foreach (EffectTemplate et in successEffects.Values)
        {
            long templateDuration = et.GetDuration2() + (long)et.GetDuration1() * GetSkillLevel();
            if (templateDuration > 0)
            {
                durationTemplate = et;
                duration = et.GetRandomTime() > 0 ? templateDuration - Rnd.Get(0, et.GetRandomTime()) : templateDuration;
                break;
            }
        }

        if (GetEffected() is Player effectedPlayer)
        {
            bool isEffectorPlayer = (CustomConfig.COUNT_SUMMON_EFFECTS_FOR_CUMULATIVE_RESIST ? effector.GetMaster() : effector) is Player;
            if (isEffectorPlayer)
            {
                duration = ApplyCumulativeResistDurationMultiplier(duration, effectedPlayer);
            }
            if (skillTemplate.GetPvpDuration() != 0 && !effector.Equals(effected))
            {
                duration = duration * skillTemplate.GetPvpDuration() / 100;
            }
        }
        if (durationTemplate is AbstractOverTimeEffect overTimeEffect)
            duration = overTimeEffect.RoundDurationToTicks(duration);
        return (int)Math.Min(int.MaxValue, duration);
    }

    private long ApplyCumulativeResistDurationMultiplier(long duration, Player effected)
    {
        foreach (EffectTemplate et in successEffects.Values)
        {
            CumulativeResistType? cumulativeResistType = et switch
            {
                FearEffect => CumulativeResistType.FEAR,
                ParalyzeEffect => CumulativeResistType.PARALYZE,
                SleepEffect => CumulativeResistType.SLEEP,
                _ => (CumulativeResistType?)null
            };
            if (cumulativeResistType != null && !et.IsNoResist())
                return effected.GetEffectController().CalculateAndApplyCumulativeResistDuration(cumulativeResistType.Value, duration);
        }
        return duration;
    }

    public bool IsDeityAvatar()
    {
        return skillTemplate.IsDeityAvatar();
    }

    public float GetX()
    {
        return x;
    }

    public float GetY()
    {
        return y;
    }

    public float GetZ()
    {
        return z;
    }

    public int GetWorldId()
    {
        return worldId;
    }

    public int GetInstanceId()
    {
        return instanceId;
    }

    public SubEffectType GetSubEffectType()
    {
        return subEffectType;
    }

    /// <summary>
    /// Client expects one byte(8bits) determining which effect positions (and sub effect type) were successful.
    /// Returns an integer containing all successful effect positions and a subeffect (if one exists).
    /// </summary>
    public byte GetSuccessfulEffectsAsByte()
    {
        if (effectResult == EffectResult.DODGE)
            return 0;
        if (effectResult == EffectResult.RESIST)
            return 1;
        byte sucEffects = 0;
        foreach (EffectTemplate effectTemplate in GetSuccessEffects())
        {
            // e1 = 1000, e2 = 11000, e3 = 111000, e4 = 1111000
            sucEffects = (byte)(sucEffects + (1 << (effectTemplate.GetPosition() + 3)));
        }
        sucEffects = (byte)(sucEffects + subEffectType.GetId());
        return sucEffects;
    }

    public void SetSubEffectType(SubEffectType subEffectType)
    {
        this.subEffectType = subEffectType;
    }

    public float GetTargetX()
    {
        return targetX;
    }

    public float GetTargetY()
    {
        return targetY;
    }

    public float GetTargetZ()
    {
        return targetZ;
    }

    public void SetTargetLoc(float x, float y, float z)
    {
        this.targetX = x;
        this.targetY = y;
        this.targetZ = z;
    }

    public void SetSubEffectAborted(bool value)
    {
        this.subEffectAbortedBySubConditions = value;
    }

    public bool IsSubEffectAbortedBySubConditions()
    {
        return this.subEffectAbortedBySubConditions;
    }

    private void AddCancelOnDmgObserver()
    {
        if (IsCancelOnDmg())
        {
            Creature effected = GetEffected();
            AddObserver(effected, new CancelOnAttackedObserver(this));
            AddObserver(effected, new CancelOnDotObserver(this));
        }
    }

    public void SetCancelOnDmg(bool value)
    {
        this.isCancelOnDmg = value;
    }

    public bool IsCancelOnDmg()
    {
        return isCancelOnDmg;
    }

    public void EndEffects()
    {
        // A selected retail numeric program can supply modifiers when the host
        // StatUp has no <change>. Keep removal with this same Effect owner.
        bool hasStatModifiers = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatBuffs.HasSelectedProgram(this);
        foreach (EffectTemplate template in successEffects.Values)
        {
            template.EndEffect(this);
            if (!hasStatModifiers && (template.GetChange() != null && template.GetChange().Count > 0 || template is AbstractAbsoluteStatEffect))
                hasStatModifiers = true;
        }
        if (hasStatModifiers)
            GetEffected().GetGameStats().EndEffect(this);
    }

    public int GetPower()
    {
        return power;
    }

    public void SetPower(int power)
    {
        this.power = power;
    }

    public int RemovePower(int power)
    {
        this.power -= power;

        return this.power;
    }

    public bool IsHideEffect()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.HIDE);
    }

    public bool IsParalyzeEffect()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.PARALYZE);
    }

    public bool IsStunEffect()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.STUN);
    }

    public bool IsSanctuaryEffect()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.SANCTUARY);
    }

    public bool IsDamageEffect()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.BACKDASH, EffectType.CARVESIGNET, EffectType.DASH, EffectType.DEATHBLOW,
            EffectType.DELAYEDSPELLATTACKINSTANT, EffectType.DISPELBUFFCOUNTERATK, EffectType.MOVEBEHIND, EffectType.NOREDUCESPELLATKINSTANT,
            EffectType.PROCATKINSTANT, EffectType.SIGNETBURST, EffectType.SKILLATKDRAININSTANT, EffectType.SKILLATTACKINSTANT,
            EffectType.SPELLATKDRAININSTANT, EffectType.SPELLATTACKINSTANT);
    }

    public bool IsNoDeathPenalty()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.NODEATHPENALTY);
    }

    public bool IsNoResurrectPenalty()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.NORESURRECTPENALTY);
    }

    public bool IsHiPass()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.HIPASS);
    }

    public bool IsDelayedDamage()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.DELAYEDSPELLATTACKINSTANT);
    }

    public bool IsSummoning()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.SUMMON, EffectType.SUMMONBINDINGGROUPGATE, EffectType.SUMMONFUNCTIONALNPC,
            EffectType.SUMMONGROUPGATE, EffectType.SUMMONHOMING, EffectType.SUMMONHOUSEGATE, EffectType.SUMMONSERVANT, EffectType.SUMMONSKILLAREA,
            EffectType.SUMMONTOTEM, EffectType.SUMMONTRAP);
    }

    public bool IsPetOrderUnSummonEffect()
    {
        return GetSkillTemplate().HasAnyEffect(EffectType.PETORDERUNSUMMON);
    }

    public bool CanRemoveOnDie()
    {
        if (GetSkillTemplate().IsNoRemoveOnDie())
            return false;
        if (Aion.GameServer.Services.Event.Event.IsEventEffectForceType(forceType))
            return false;
        if (GetSkillTemplate().HasAnyEffect(EffectType.XPBOOST))
            return false;
        return true;
    }

    public int GetSignetBurstedCount()
    {
        return signetBurstedCount;
    }

    public void SetSignetBurstedCount(int signetBurstedCount)
    {
        this.signetBurstedCount = signetBurstedCount;
    }

    public EffectResult GetEffectResult()
    {
        return effectResult;
    }

    public void SetEffectResult(EffectResult effectResult)
    {
        this.effectResult = effectResult;
    }

    internal bool RetailHasEnded => hasEnded.Get();
    internal void SetRetailPeriodicDeadline(long value) => endTime=value;

    public bool IsEndedByTime()
    {
        return endedByTime;
    }

    public int GetMpAbsorbed()
    {
        return mpAbsorbed;
    }

    public void SetMpAbsorbed(int mpAbsorbed)
    {
        this.mpAbsorbed = mpAbsorbed;
    }

    public int GetMpShieldSkillId()
    {
        return mpShieldSkillId;
    }

    public void SetMpShieldSkillId(int mpShieldSkillId)
    {
        this.mpShieldSkillId = mpShieldSkillId;
    }

    public HashSet<EffectType> GetPossibleConflictEffectTypes()
    {
        return skillTemplate.GetEffects() == null
            ? new HashSet<EffectType>()
            : skillTemplate.GetEffects().GetPossibleConflictEffectTypes();
    }

    public sealed class ForceType
    {
        private static readonly ConcurrentDictionary<string, ForceType> forceTypes = new ConcurrentDictionary<string, ForceType>();
        public static readonly ForceType DEFAULT = GetInstance("");
        public static readonly ForceType MATERIAL_SKILL = GetInstance("MATERIAL_SKILL");
        private readonly string name;

        private ForceType(string name)
        {
            this.name = name;
        }

        public string GetName()
        {
            return name;
        }

        public static ForceType GetInstance(string name)
        {
            return forceTypes.GetOrAdd(name, key => new ForceType(key));
        }
    }

    public Effect GetDesignatedDispelEffect()
    {
        return designatedDispelEffect;
    }

    public bool SetDesignatedDispelEffect(Effect effect)
    {
        if (designatedDispelEffect == null)
        {
            designatedDispelEffect = effect;
            return true;
        }
        return false;
    }

    public void ResetDesignatedDispelEffect()
    {
        designatedDispelEffect = null;
    }

    public bool IsSubEffect()
    {
        return isSubEffect;
    }

    public bool TryActivateGodstone()
    {
        return allowGodstoneActivation.CompareAndSet(true, false);
    }

    // Java parity: anonymous ActionObserver (ObserverType.ATTACKED) in addCancelOnDmgObserver.
    private sealed class CancelOnAttackedObserver : ActionObserver
    {
        private readonly Effect _effect;
        public CancelOnAttackedObserver(Effect effect) : base(ObserverType.ATTACKED) { _effect = effect; }
        public override void Attacked(Creature creature, int skillId) { _effect.EndEffect(); }
    }

    // Java parity: anonymous ActionObserver (ObserverType.DOT_ATTACKED) in addCancelOnDmgObserver.
    private sealed class CancelOnDotObserver : ActionObserver
    {
        private readonly Effect _effect;
        public CancelOnDotObserver(Effect effect) : base(ObserverType.DOT_ATTACKED) { _effect = effect; }
        public override void Dotattacked(Creature creature, Effect dotEffect) { _effect.EndEffect(); }
    }
}
