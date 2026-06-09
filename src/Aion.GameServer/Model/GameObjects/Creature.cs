using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Effect;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effect;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Represents movable objects; base class for all in-game objects that may move.
/// Java parity: model/gameobjects/Creature (@author -Nemesiss-).
/// </summary>
/// <remarks>
/// Wildcard-generic Java fields (<c>CreatureGameStats&lt;? extends Creature&gt;</c> etc.) are held as the non-generic
/// bases (see CreatureGameStats/CreatureLifeStats split). AI/move/controller use the same non-generic-base pattern
/// (their bases are still red until ported).
/// </remarks>
public abstract class Creature : VisibleObject
{
    private readonly Aion.GameServer.Ai.AbstractAI ai;
    private CreatureGameStats gameStats;
    private CreatureLifeStats lifeStats;
    private EffectController effectController;
    protected Aion.GameServer.Controllers.Movement.CreatureMoveController moveController;
    private int state = CreatureState.Active.GetId();
    private int visualState = CreatureVisualState.Visible.GetId();
    private int seeState = CreatureSeeState.Normal.GetId();
    private Skill castingSkill;
    private ConcurrentDictionary<int, long> skillCoolDowns;
    private ObserveController observeController;
    private TransformModel transformModel;
    private readonly AggroList aggroList;
    private readonly byte[] zoneTypes = new byte[Enum.GetValues(typeof(Aion.GameServer.Model.Templates.Zone.ZoneType)).Length];
    private int skillNumber;
    private int attackedCount;
    private long spawnTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static long CurrentTimeMillis() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public Creature(int objId, CreatureController controller, Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, CreatureTemplate objectTemplate,
        WorldPosition position, bool autoReleaseObjectId)
        : base(objId, controller, spawnTemplate, objectTemplate, position, autoReleaseObjectId)
    {
        string aiName = objectTemplate.GetAiName();
        if (spawnTemplate != null && spawnTemplate.GetAiName() != null)
            aiName = Aion.GameServer.Model.Templates.Spawns.SpawnTemplate.NO_AI.Equals(spawnTemplate.GetAiName()) ? null : spawnTemplate.GetAiName();
        this.ai = Aion.GameServer.Ai.AIEngine.GetInstance().NewAI(aiName, this);
        this.observeController = new ObserveController();
        this.aggroList = CreateAggroList();
    }

    public virtual Aion.GameServer.Controllers.Movement.CreatureMoveController GetMoveController()
    {
        return moveController;
    }

    protected virtual AggroList CreateAggroList()
    {
        return new AggroList(this);
    }

    public override CreatureController GetController()
    {
        return (CreatureController)base.GetController();
    }

    public virtual CreatureLifeStats GetLifeStats()
    {
        return lifeStats;
    }

    public void SetLifeStats(CreatureLifeStats lifeStats)
    {
        this.lifeStats = lifeStats;
    }

    public virtual CreatureGameStats GetGameStats()
    {
        return gameStats;
    }

    public void SetGameStats(CreatureGameStats gameStats)
    {
        this.gameStats = gameStats;
    }

    public abstract sbyte GetLevel();

    public virtual EffectController GetEffectController()
    {
        return effectController;
    }

    public void SetEffectController(EffectController effectController)
    {
        this.effectController = effectController;
    }

    public Aion.GameServer.Ai.AbstractAI GetAi()
    {
        return ai;
    }

    public bool IsDead()
    {
        return lifeStats.IsDead();
    }

    /// <summary>True if the creature is a flag (symbol on map).</summary>
    public virtual bool IsFlag()
    {
        return false;
    }

    public bool IsCasting()
    {
        return castingSkill != null;
    }

    /// <summary>Set current casting skill or null when skill ends.</summary>
    public virtual void SetCasting(Skill castingSkill)
    {
        if (castingSkill != null)
            skillNumber++;
        this.castingSkill = castingSkill;
    }

    public int GetCastingSkillId()
    {
        return castingSkill != null ? castingSkill.GetSkillTemplate().GetSkillId() : 0;
    }

    public Skill GetCastingSkill()
    {
        return castingSkill;
    }

    public int GetSkillNumber()
    {
        return skillNumber;
    }

    public void SetSkillNumber(int skillNumber)
    {
        this.skillNumber = skillNumber;
    }

    public int GetAttackedCount()
    {
        return this.attackedCount;
    }

    public void IncrementAttackedCount()
    {
        this.attackedCount++;
    }

    public void ClearAttackedCount()
    {
        attackedCount = 0;
    }

    /// <summary>All abnormal effects are checked that disable movements.</summary>
    public bool CanPerformMove()
    {
        return (!(GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_MOVE_STATE) && IsSpawned() && CanUseSkillInMove()));
    }

    private bool CanUseSkillInMove()
    {
        if (castingSkill != null)
        {
            SkillTemplate st = DataManager.SKILL_DATA.GetSkillTemplate(castingSkill.GetSkillId());
            if (st.GetStartconditions() != null && st.GetMovedCondition() != null)
            {
                if (!st.GetMovedCondition().IsAllow())
                    return false;
            }
        }
        return true;
    }

    /// <summary>All abnormal effects are checked that disable attack.</summary>
    public bool CanAttack()
    {
        return (!GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE) && !IsCasting() && !IsInState(CreatureState.Resting)
            && !IsInState(CreatureState.PrivateShop));
    }

    public int GetState()
    {
        return state;
    }

    /// <summary>Sets the given state while keeping all present ones.</summary>
    public void SetState(CreatureState state)
    {
        SetState(state, false);
    }

    /// <summary>Sets the given state. If <paramref name="replace"/> is true, previous states are completely replaced.</summary>
    public void SetState(CreatureState state, bool replace)
    {
        if (replace)
            this.state = state.GetId();
        else
            this.state |= state.GetId();
    }

    /// <summary><paramref name="state"/> taken usually from templates.</summary>
    public void SetState(int state)
    {
        this.state = state;
    }

    public void UnsetState(CreatureState state)
    {
        this.state &= ~state.GetId();
    }

    public bool IsInState(CreatureState state)
    {
        if (state.MustMatchExact())
            return this.state == state.GetId();
        else
            return (this.state & state.GetId()) == state.GetId();
    }

    public int GetVisualState()
    {
        return visualState;
    }

    public void SetVisualState(CreatureVisualState visualState)
    {
        this.visualState |= visualState.GetId();
    }

    public void UnsetVisualState(CreatureVisualState visualState)
    {
        this.visualState &= ~visualState.GetId();
    }

    public bool IsInVisualState(CreatureVisualState visualState)
    {
        return (this.visualState & visualState.GetId()) == visualState.GetId();
    }

    public bool IsInAnyHide()
    {
        return visualState != CreatureVisualState.Visible.GetId() && visualState != CreatureVisualState.Blinking.GetId();
    }

    public virtual int GetSeeState()
    {
        return seeState;
    }

    public void SetSeeState(CreatureSeeState seeState)
    {
        this.seeState |= seeState.GetId();
    }

    public void UnsetSeeState(CreatureSeeState seeState)
    {
        this.seeState &= ~seeState.GetId();
    }

    public bool IsInSeeState(CreatureSeeState seeState)
    {
        int isSeeState = this.seeState & seeState.GetId();

        if (isSeeState == seeState.GetId())
            return true;

        return false;
    }

    public TransformModel GetTransformModel()
    {
        if (transformModel == null)
            transformModel = new TransformModel(this);
        return transformModel;
    }

    public void EndTransformation()
    {
        GetTransformModel().Apply(0);
    }

    public bool IsTransformed()
    {
        return transformModel != null && GetTransformModel().IsActive();
    }

    public AggroList GetAggroList()
    {
        return aggroList;
    }

    public ObserveController GetObserveController()
    {
        return observeController;
    }

    public virtual bool IsEnemy(Creature creature)
    {
        return creature.IsEnemyFrom(this);
    }

    public virtual bool IsEnemyFrom(Creature creature)
    {
        return false;
    }

    public virtual bool IsEnemyFrom(Player.Player player)
    {
        return false;
    }

    public virtual bool IsEnemyFrom(Npc npc)
    {
        return false;
    }

    public virtual Aion.GameServer.Model.TribeClass GetTribe()
    {
        return Aion.GameServer.Model.TribeClass.GENERAL;
    }

    public virtual Aion.GameServer.Model.TribeClass GetBaseTribe()
    {
        return Aion.GameServer.Model.TribeClass.GENERAL;
    }

    public override bool CanSee(VisibleObject obj)
    {
        if (obj is Creature creature)
        {
            int visualStateExcludingBlinking = creature.GetVisualState() & ~CreatureVisualState.Blinking.GetId();
            if (visualStateExcludingBlinking <= GetSeeState())
                return true;
            return Equals(creature.GetMaster()); // traps, summons, etc. should always be visible to the master
        }
        else if (obj is Pet pet)
        {
            // we must prevent sending the pet's spawn packet to others before the master's, as this causes the pet to stay invisible
            return Equals(pet.GetMaster()) || CanSee(pet.GetMaster()) && GetKnownList().Sees(pet.GetMaster());
        }
        return base.CanSee(obj);
    }

    /// <summary>Returns NpcObjectType.NORMAL.</summary>
    public virtual NpcObjectType GetNpcObjectType()
    {
        return NpcObjectType.NORMAL;
    }

    /// <summary>
    /// For summons and different kinds of servants it returns currently acting player. Used for duel/enemy relations and rewards.
    /// Returns master of this creature, or self.
    /// </summary>
    public virtual Creature GetMaster()
    {
        return this;
    }

    /// <summary>
    /// For summons returns summon object and for servants returns player object. Used to find attackable target for npcs.
    /// Returns acting master - player in case of servants.
    /// </summary>
    public virtual Creature GetActingCreature()
    {
        return GetMaster();
    }

    public bool IsSkillDisabled(SkillTemplate template)
    {
        if (skillCoolDowns == null)
            return false;

        int cooldownId = template.GetCooldownId();
        if (!skillCoolDowns.TryGetValue(cooldownId, out long coolDown))
        {
            return false;
        }

        if (coolDown < CurrentTimeMillis())
        {
            RemoveSkillCoolDown(cooldownId);
            return false;
        }
        return true;
    }

    public long GetSkillCoolDown(int cooldownId)
    {
        return skillCoolDowns == null ? 0L : (skillCoolDowns.TryGetValue(cooldownId, out long v) ? v : 0L);
    }

    public void SetSkillCoolDown(int cooldownId, long time)
    {
        if (cooldownId == 0)
        {
            return;
        }
        if (skillCoolDowns == null)
            skillCoolDowns = new ConcurrentDictionary<int, long>();
        skillCoolDowns[cooldownId] = time;
    }

    public ConcurrentDictionary<int, long> GetSkillCoolDowns()
    {
        return skillCoolDowns;
    }

    public void RemoveSkillCoolDown(int cooldownId)
    {
        if (skillCoolDowns == null)
            return;
        skillCoolDowns.TryRemove(cooldownId, out _);
    }

    /// <summary>True if this creature can not receive any damage.</summary>
    public virtual bool IsInvulnerable()
    {
        return false;
    }

    public virtual Aion.GameServer.Model.Templates.Item.ItemAttackType GetAttackType()
    {
        return Aion.GameServer.Model.Templates.Item.ItemAttackType.PHYSICAL;
    }

    /// <summary>Creature is flying (FLY or GLIDE states).</summary>
    public bool IsFlying()
    {
        return (IsInState(CreatureState.Flying) && !IsInState(CreatureState.Resting)) || IsInState(CreatureState.Gliding);
    }

    public bool IsInFlyingState()
    {
        return IsInState(CreatureState.Flying) && !IsInState(CreatureState.Resting);
    }

    public virtual bool IsPvpTarget(Creature creature)
    {
        return false;
    }

    /// <summary>All zones the creature currently is in (even if not currently spawned).</summary>
    public List<Aion.GameServer.World.Zone.ZoneInstance> FindZones()
    {
        return GetPosition().GetMapRegion() == null ? new List<Aion.GameServer.World.Zone.ZoneInstance>() : GetPosition().GetMapRegion().FindZones(this);
    }

    public void RevalidateZones()
    {
        if (!IsSpawned())
            return;
        MapRegion mapRegion = GetPosition().GetMapRegion();
        if (mapRegion != null)
            mapRegion.RevalidateZones(this);
    }

    public bool IsInsideZone(Aion.GameServer.World.Zone.ZoneName zoneName)
    {
        if (!IsSpawned())
            return false;
        return GetPosition().GetMapRegion().IsInsideZone(zoneName, this);
    }

    public bool IsInsideItemUseZone(Aion.GameServer.World.Zone.ZoneName zoneName)
    {
        if (!IsSpawned())
            return false;
        return GetPosition().GetMapRegion().IsInsideItemUseZone(zoneName, this);
    }

    /// <summary>Increments an internal counter for the given zone type, to support nested zones.</summary>
    public void SetInsideZoneType(Aion.GameServer.Model.Templates.Zone.ZoneType zoneType)
    {
        lock (zoneTypes)
        {
            zoneTypes[(int)zoneType]++;
        }
    }

    /// <summary>Decrements an internal counter for the given zone type, to support nested zones.</summary>
    public void UnsetInsideZoneType(Aion.GameServer.Model.Templates.Zone.ZoneType zoneType)
    {
        lock (zoneTypes)
        {
            zoneTypes[(int)zoneType]--;
        }
    }

    /// <summary>True if the creature is inside one or more zones of the specified type.</summary>
    public bool IsInsideZoneType(Aion.GameServer.Model.Templates.Zone.ZoneType zoneType)
    {
        lock (zoneTypes)
        {
            return zoneTypes[(int)zoneType] > 0;
        }
    }

    public bool IsInsidePvPZone()
    {
        lock (zoneTypes)
        {
            if (zoneTypes[(int)Aion.GameServer.Model.Templates.Zone.ZoneType.SIEGE] > 0)
            {
                return true;
            }
            int pvpValue = zoneTypes[(int)Aion.GameServer.Model.Templates.Zone.ZoneType.PVP];
            return pvpValue == 0 || pvpValue == 2;
        }
    }

    public virtual Aion.GameServer.Model.Race GetRace()
    {
        return Aion.GameServer.Model.Race.NONE;
    }

    public virtual int GetSkillCooldown(SkillTemplate template)
    {
        return template.GetCooldown();
    }

    public long GetMillisSinceSpawn()
    {
        return CurrentTimeMillis() - spawnTime;
    }

    public bool IsNewSpawn()
    {
        return GetMillisSinceSpawn() < 1500;
    }

    public virtual bool IsRaidMonster()
    {
        return false;
    }

    public bool IsWorldRaidMonster()
    {
        return GetTribe() == Aion.GameServer.Model.TribeClass.WORLDRAID_MONSTER || GetTribe() == Aion.GameServer.Model.TribeClass.WORLDRAID_MONSTER_SANDWORMSUM && IsRaidMonster();
    }

    public virtual Aion.GameServer.Model.Items.NpcEquippedGear GetOverrideEquipment()
    {
        return null;
    }
}
