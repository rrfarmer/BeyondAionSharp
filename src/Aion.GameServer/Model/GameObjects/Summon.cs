using System.Collections.Concurrent;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/Summon extends Creature (@author ATracer).</summary>
public class Summon : Creature
{
    private readonly Player master;
    private SummonMode mode = SummonMode.GUARD;
    private readonly ConcurrentQueue<SkillOrder> skillOrders = new ConcurrentQueue<SkillOrder>();
    private ScheduledTask releaseTask;
    private SkillElement alwaysResistElement = SkillElement.NONE;
    private int summonedBySkillId, liveTime;

    public Summon(int objId, Aion.GameServer.Controllers.SummonController controller, Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, Aion.GameServer.Model.Templates.Npc.NpcTemplate objectTemplate, Player master, int time)
        : base(objId, controller, spawnTemplate, objectTemplate, new WorldPosition(spawnTemplate.GetWorldId()), true)
    {
        controller.SetOwner(this);
        moveController = controller is Aion.GameServer.Controllers.SiegeWeaponController ? new Aion.GameServer.Controllers.Movement.SiegeWeaponMoveController(this) : new Aion.GameServer.Controllers.Movement.SummonMoveController(this);
        this.liveTime = time;
        this.master = master;
        SetGameStats(new SummonGameStats(this));
        SetLifeStats(new SummonLifeStats(this));
        SetAlwaysResistElement(objectTemplate);
    }

    private void SetAlwaysResistElement(Aion.GameServer.Model.Templates.Npc.NpcTemplate template)
    {
        if (template != null)
        {
            switch (template.GetName())
            {
                case "earth spirit":
                    this.alwaysResistElement = SkillElement.EARTH;
                    break;
                case "fire spirit":
                    this.alwaysResistElement = SkillElement.FIRE;
                    break;
                case "water spirit":
                    this.alwaysResistElement = SkillElement.WATER;
                    break;
                case "wind spirit":
                    this.alwaysResistElement = SkillElement.WIND;
                    break;
            }
        }
    }

    protected override AggroList CreateAggroList()
    {
        return new PlayerAggroList(this);
    }

    public override SummonGameStats GetGameStats()
    {
        return (SummonGameStats)base.GetGameStats();
    }

    public override Player GetMaster()
    {
        return master;
    }

    public override sbyte GetLevel()
    {
        return GetObjectTemplate().GetLevel();
    }

    public override Aion.GameServer.Model.Templates.Npc.NpcTemplate GetObjectTemplate()
    {
        return (Aion.GameServer.Model.Templates.Npc.NpcTemplate)base.GetObjectTemplate();
    }

    public int GetNpcId()
    {
        return GetObjectTemplate().GetTemplateId();
    }

    public string GetL10n()
    {
        return GetObjectTemplate().GetL10n();
    }

    public override NpcObjectType GetNpcObjectType()
    {
        return NpcObjectType.SUMMON;
    }

    public override Aion.GameServer.Controllers.SummonController GetController()
    {
        return (Aion.GameServer.Controllers.SummonController)base.GetController();
    }

    public SummonMode GetMode()
    {
        return mode;
    }

    public void SetMode(SummonMode mode)
    {
        if (mode != SummonMode.ATTACK)
            ClearSkillOrders();
        this.mode = mode;
    }

    public override bool IsEnemy(Creature creature)
    {
        return master.IsEnemy(creature);
    }

    public override bool IsEnemyFrom(Npc npc)
    {
        return master.IsEnemyFrom(npc);
    }

    public override bool IsEnemyFrom(Player player)
    {
        return master.IsEnemyFrom(player);
    }

    public override bool IsPvpTarget(Creature creature)
    {
        return creature.GetActingCreature() is Player;
    }

    public override TribeClass GetTribe()
    {
        return master.GetTribe();
    }

    // Java parity: getType(Creature) — renamed to avoid Object.GetType clash.
    public CreatureType GetTypeValue(Creature creature)
    {
        bool friend = master.GetRace() == creature.GetRace() && !creature.IsEnemy(master);
        return friend ? CreatureType.SUPPORT : CreatureType.ATTACKABLE;
    }

    // Java parity: getType(Creature) - GetType_ is the project-wide getType() convention name.
    public CreatureType GetType_(Creature creature) => GetTypeValue(creature);

    public override Aion.GameServer.Controllers.Movement.SummonMoveController GetMoveController()
    {
        return (Aion.GameServer.Controllers.Movement.SummonMoveController)base.GetMoveController();
    }

    public override Player GetActingCreature()
    {
        return GetMaster();
    }

    public override Race GetRace()
    {
        return GetMaster().GetRace();
    }

    public bool IsPet()
    {
        return GetObjectTemplate().GetNpcTemplateType() == Aion.GameServer.Model.Templates.Npc.NpcTemplateType.SUMMON_PET;
    }

    /// <summary>liveTime in sec.</summary>
    public int GetLiveTime()
    {
        return liveTime;
    }

    public void SetLiveTime(int liveTime)
    {
        this.liveTime = liveTime;
    }

    public int GetSummonedBySkillId()
    {
        return summonedBySkillId;
    }

    public void SetSummonedBySkillId(int summonedBySkillId)
    {
        this.summonedBySkillId = summonedBySkillId;
    }

    public void SetReleaseTask(ScheduledTask task)
    {
        releaseTask = task;
    }

    public void CancelReleaseTask()
    {
        if (releaseTask != null && !releaseTask.Completion.IsCompleted)
        {
            releaseTask.Cancel();
        }
    }

    public void AddSkillOrder(int skillId, int skillLvl, Creature target, int hate, bool release)
    {
        skillOrders.Enqueue(new SkillOrder(skillId, skillLvl, target, hate, release));
    }

    public SkillOrder RetrieveNextSkillOrder()
    {
        return skillOrders.TryDequeue(out SkillOrder o) ? o : null;
    }

    public SkillOrder GetNextSkillOrder()
    {
        return skillOrders.TryPeek(out SkillOrder o) ? o : null;
    }

    public void ClearSkillOrders()
    {
        skillOrders.Clear();
    }

    public SkillElement GetAlwaysResistElement()
    {
        return alwaysResistElement;
    }
}
