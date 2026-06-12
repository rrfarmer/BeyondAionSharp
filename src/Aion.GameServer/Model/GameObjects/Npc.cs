using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Base class for all in-game NPCs (monsters and citizens). Java parity: model/gameobjects/Npc (@author Luno).
/// </summary>
public class Npc : Creature
{
    private readonly Aion.GameServer.Model.Skill.NpcSkillList skillList;
    private readonly Queue<Aion.GameServer.Model.Skill.NpcSkillEntry> queuedSkills = new Queue<Aion.GameServer.Model.Skill.NpcSkillEntry>();
    private Aion.GameServer.SpawnEngine.WalkerGroup walkerGroup;
    private string masterName;
    private int creatorId = 0;
    private CreatureType? type = null;
    private Aion.GameServer.Model.Items.NpcEquippedGear overridenEquipment;
    private SummonOwner summonOwner = null;

    public Npc(Aion.GameServer.Controllers.NpcController controller, Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, Aion.GameServer.Model.Templates.Npc.NpcTemplate objectTemplate)
        : base(Aion.GameServer.Utils.IdFactory.IDFactory.GetInstance().NextId(), controller, spawnTemplate, objectTemplate, new WorldPosition(spawnTemplate.GetWorldId()), true)
    {
        // Java: Objects.requireNonNull(objectTemplate) before super (C# cannot run code before base()).
        controller.SetOwner(this);
        moveController = new Aion.GameServer.Controllers.Movement.NpcMoveController(this);
        skillList = new Aion.GameServer.Model.Skill.NpcSkillList(this);
        SetupStatContainers();
    }

    public override Aion.GameServer.Controllers.Movement.NpcMoveController GetMoveController()
    {
        return (Aion.GameServer.Controllers.Movement.NpcMoveController)base.GetMoveController();
    }

    protected virtual void SetupStatContainers()
    {
        SetGameStats(new NpcGameStats(this));
        SetLifeStats(new NpcLifeStats(this));
    }

    public override Aion.GameServer.Model.Templates.Npc.NpcTemplate GetObjectTemplate()
    {
        return (Aion.GameServer.Model.Templates.Npc.NpcTemplate)base.GetObjectTemplate();
    }

    public int GetNpcId()
    {
        return GetObjectTemplate().GetTemplateId();
    }

    public override sbyte GetLevel()
    {
        return GetObjectTemplate().GetLevel();
    }

    public Aion.GameServer.Model.Templates.Npc.AbyssNpcType GetAbyssNpcType()
    {
        return GetObjectTemplate().GetAbyssNpcType();
    }

    public Aion.GameServer.Model.Templates.Npc.NpcRating GetRating()
    {
        return GetObjectTemplate().GetRating();
    }

    public Aion.GameServer.Model.Templates.Npc.NpcRank GetRank()
    {
        return GetObjectTemplate().GetRank();
    }

    public Aion.GameServer.Model.Templates.Npc.NpcTemplateType GetNpcTemplateType()
    {
        return GetObjectTemplate().GetNpcTemplateType();
    }

    public int GetHpGauge()
    {
        return GetObjectTemplate().GetHpGauge();
    }

    public override NpcLifeStats GetLifeStats()
    {
        return (NpcLifeStats)base.GetLifeStats();
    }

    public override NpcGameStats GetGameStats()
    {
        return (NpcGameStats)base.GetGameStats();
    }

    public override Aion.GameServer.Controllers.NpcController GetController()
    {
        return (Aion.GameServer.Controllers.NpcController)base.GetController();
    }

    public override Aion.GameServer.Model.Templates.Items.ItemAttackType GetAttackType()
    {
        return GetAi().ModifyAttackType(Aion.GameServer.Model.Templates.Items.ItemAttackType.PHYSICAL);
    }

    public Aion.GameServer.Model.Skill.NpcSkillList GetSkillList()
    {
        return skillList;
    }

    public Aion.GameServer.Model.Skill.NpcSkillEntry GetNextQueuedSkill()
    {
        lock (queuedSkills)
        {
            return queuedSkills.Count == 0 ? null : queuedSkills.Peek();
        }
    }

    public bool HasQueuedSkill(Predicate<Aion.GameServer.Model.Skill.NpcSkillEntry> filter)
    {
        lock (queuedSkills)
        {
            return queuedSkills.Any(s => filter(s));
        }
    }

    public void RemoveNextQueuedSkill(Aion.GameServer.Model.Skill.NpcSkillEntry skill)
    {
        lock (queuedSkills)
        {
            if (queuedSkills.Count > 0 && queuedSkills.Peek() == skill)
            {
                queuedSkills.Dequeue();
            }
        }
    }

    public void ClearQueuedSkills()
    {
        lock (queuedSkills)
        {
            queuedSkills.Clear();
        }
    }

    public void QueueSkill(Aion.GameServer.Model.Skill.NpcSkillEntry skill)
    {
        lock (queuedSkills)
        {
            queuedSkills.Enqueue(skill);
        }
    }

    public void QueueSkill(int skillId, int level)
    {
        QueueSkill(new Aion.GameServer.Model.Skill.NpcSkillTemplateEntry(new Aion.GameServer.Model.Templates.Npcskill.QueuedNpcSkillTemplate(skillId, level)));
    }

    public void QueueSkill(int skillId, int level, int nextSkillTime)
    {
        QueueSkill(new Aion.GameServer.Model.Skill.NpcSkillTemplateEntry(new Aion.GameServer.Model.Templates.Npcskill.QueuedNpcSkillTemplate(skillId, level, nextSkillTime, Aion.GameServer.Model.Templates.Npcskill.NpcSkillTargetAttribute.MOST_HATED)));
    }

    public void QueueSkill(int skillId, int level, int nextSkillTime, Aion.GameServer.Model.Templates.Npcskill.NpcSkillTargetAttribute npcSkillTargetAttribute)
    {
        QueueSkill(new Aion.GameServer.Model.Skill.NpcSkillTemplateEntry(new Aion.GameServer.Model.Templates.Npcskill.QueuedNpcSkillTemplate(skillId, level, nextSkillTime, npcSkillTargetAttribute)));
    }

    public bool IsWalker()
    {
        return IsRandomWalker() || IsPathWalker();
    }

    public bool IsRandomWalker()
    {
        return GetSpawn().GetRandomWalkRange() > 0;
    }

    public bool IsPathWalker()
    {
        return GetSpawn().GetWalkerId() != null;
    }

    public override TribeClass GetTribe()
    {
        if (GetCreator() is Player player)
            return player.GetTribe();
        TribeClass? transformTribe = IsTransformed() ? GetTransformModel().GetTribe() : (TribeClass?)null;
        if (transformTribe != null)
        {
            return transformTribe.Value;
        }
        return GetObjectTemplate().GetTribe();
    }

    public override TribeClass GetBaseTribe()
    {
        return DataManager.TRIBE_RELATIONS_DATA.GetBaseTribe(GetTribe());
    }

    public int GetAggroRange()
    {
        return GetAi().ModifyAggroRange(GetObjectTemplate().GetAggroRange());
    }

    public int GetShortAggroRange()
    {
        int aggroRange = GetAggroRange();
        return aggroRange < 8 ? aggroRange / 2 : 4;
    }

    public int GetAggroAngle()
    {
        return GetAi().ModifyAggroAngle(GetObjectTemplate().GetAggroAngle());
    }

    /// <summary>True if the npc is within 1m of its spawn location.</summary>
    public bool IsAtSpawnLocation()
    {
        return PositionUtil.IsInRange(this, GetSpawn().GetX(), GetSpawn().GetY(), GetSpawn().GetZ(), 1);
    }

    public override bool IsEnemy(Creature creature)
    {
        return creature.IsEnemyFrom(this) || this.IsEnemyFrom(creature);
    }

    public override bool IsEnemyFrom(Creature creature)
    {
        return Aion.GameServer.Services.TribeRelationService.IsAggressive(creature, this) || Aion.GameServer.Services.TribeRelationService.IsHostile(creature, this);
    }

    public override bool IsEnemyFrom(Npc npc)
    {
        return Aion.GameServer.Services.TribeRelationService.IsAggressive(this, npc) || Aion.GameServer.Services.TribeRelationService.IsHostile(this, npc);
    }

    public override bool IsEnemyFrom(Player player)
    {
        return player.IsEnemyFrom(this);
    }

    // Java parity: getType(Creature) — renamed to avoid Object.GetType clash.
    // Java parity: getType(Creature) - GetType_ is the project-wide getType() convention name.
    public CreatureType GetType_(Creature creature) => GetTypeValue(creature);

    public virtual CreatureType GetTypeValue(Creature creature)
    {
        if (type != null)
            return type.Value;
        if (Aion.GameServer.Services.TribeRelationService.IsNone(this, creature))
            return CreatureType.PEACE;
        else if (Aion.GameServer.Services.TribeRelationService.IsAggressive(this, creature))
            return CreatureType.AGGRESSIVE;
        else if (Aion.GameServer.Services.TribeRelationService.IsHostile(this, creature))
            return CreatureType.ATTACKABLE;
        else if (Aion.GameServer.Services.TribeRelationService.IsFriend(this, creature) || Aion.GameServer.Services.TribeRelationService.IsNeutral(this, creature))
            return CreatureType.FRIEND;
        else if (Aion.GameServer.Services.TribeRelationService.IsSupport(this, creature))
            return CreatureType.SUPPORT;
        return CreatureType.ATTACKABLE;
    }

    /// <summary>Sets a constant type and broadcasts it, if the npc is spawned. Set to null to disable it.</summary>
    public void OverrideNpcType(CreatureType? newType)
    {
        type = newType;
        if (IsSpawned())
        {
            if (type != null)
                PacketSendUtility.BroadcastPacket(this, new Aion.GameServer.Network.Aion.ServerPackets.SM_CUSTOM_SETTINGS(GetObjectId(), 0, type.Value.GetId(), 0));
            else
                GetKnownList().ForEachPlayer(p => PacketSendUtility.SendPacket(p, new Aion.GameServer.Network.Aion.ServerPackets.SM_CUSTOM_SETTINGS(GetObjectId(), 0, GetTypeValue(p).GetId(), 0)));
        }
    }

    /// <summary>Distance to spawn location.</summary>
    public double GetDistanceToSpawnLocation()
    {
        return PositionUtil.GetDistance(GetSpawn().GetX(), GetSpawn().GetY(), GetSpawn().GetZ(), GetX(), GetY(), GetZ());
    }

    public override int GetSeeState()
    {
        int skillSeeState = base.GetSeeState();
        int congenitalSeeState = GetObjectTemplate().GetRating().GetCongenitalSeeState().GetId();
        return Math.Max(skillSeeState, congenitalSeeState);
    }

    public virtual string GetMasterName()
    {
        return masterName;
    }

    public void SetMasterName(string masterName)
    {
        this.masterName = masterName;
    }

    public virtual int GetCreatorId()
    {
        return creatorId;
    }

    public void SetCreatorId(int creatorId)
    {
        this.creatorId = creatorId;
    }

    public virtual VisibleObject GetCreator()
    {
        return creatorId == 0 ? null : World.World.GetInstance().FindVisibleObject(creatorId);
    }

    public void SetWalkerGroup(Aion.GameServer.SpawnEngine.WalkerGroup wg)
    {
        this.walkerGroup = wg;
    }

    public Aion.GameServer.SpawnEngine.WalkerGroup GetWalkerGroup()
    {
        return walkerGroup;
    }

    public override bool IsFlag()
    {
        return GetObjectTemplate().GetNpcTemplateType() == Aion.GameServer.Model.Templates.Npc.NpcTemplateType.FLAG;
    }

    public override bool IsRaidMonster()
    {
        return GetObjectTemplate().GetNpcTemplateType() == Aion.GameServer.Model.Templates.Npc.NpcTemplateType.RAID_MONSTER;
    }

    public bool IsBoss()
    {
        return GetObjectTemplate().GetRating() == Aion.GameServer.Model.Templates.Npc.NpcRating.HERO || GetObjectTemplate().GetRating() == Aion.GameServer.Model.Templates.Npc.NpcRating.LEGENDARY;
    }

    public bool HasStatic()
    {
        return GetSpawn().GetStaticId() != 0;
    }

    public override Race GetRace()
    {
        return GetObjectTemplate().GetRace();
    }

    /// <summary>True if this npc sells items.</summary>
    public bool CanSell()
    {
        return DataManager.TRADE_LIST_DATA.GetTradeListTemplate(GetNpcId()) != null && GetObjectTemplate().SupportsAction(DialogAction.BUY);
    }

    /// <summary>True if this npc buys items.</summary>
    public bool CanBuy()
    {
        return GetObjectTemplate().SupportsAction(DialogAction.SELL) || CanSell();
    }

    /// <summary>True if this npc trades items for other items.</summary>
    public bool CanTradeIn()
    {
        return DataManager.TRADE_LIST_DATA.GetTradeInListTemplate(GetNpcId()) != null && GetObjectTemplate().SupportsAction(DialogAction.TRADE_IN);
    }

    /// <summary>True if this npc buys specific items.</summary>
    public bool CanPurchase()
    {
        return DataManager.TRADE_LIST_DATA.GetPurchaseTemplate(GetNpcId()) != null && GetObjectTemplate().SupportsAction(DialogAction.TRADE_SELL_LIST);
    }

    public Aion.GameServer.Model.Templates.Npc.GroupDropType GetGroupDrop()
    {
        return GetObjectTemplate().GetGroupDrop();
    }

    public void OverrideEquipmentList(Aion.GameServer.Dataholders.LoadingUtils.Adapters.NpcEquipmentList v)
    {
        overridenEquipment = new Aion.GameServer.Model.Items.NpcEquippedGear(v);
    }

    public override Aion.GameServer.Model.Items.NpcEquippedGear GetOverrideEquipment()
    {
        if (overridenEquipment != null)
            return overridenEquipment;
        return GetObjectTemplate().GetEquipment();
    }

    public void SetSummonOwner(SummonOwner summonOwner)
    {
        this.summonOwner = summonOwner;
    }

    public SummonOwner GetSummonOwner()
    {
        return summonOwner;
    }
}
