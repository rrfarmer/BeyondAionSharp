using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("orissan")]
public class OrissanAI : AggressiveNoLootNpcAI
{
    private readonly AtomicBoolean isActive = new AtomicBoolean();
    private ScheduledTask task;
    private int lastSpawnEvent; // 1 == Frigid Crystals, 2 == Icing Crystals

    public OrissanAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        lastSpawnEvent = Rnd.Get(1, 2);
        // task = ThreadPoolManager.getInstance().schedule(this::startSummoningEvent, 30000);
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        // if (isActive.compareAndSet(false, true))
        // task = ThreadPoolManager.getInstance().schedule(this::startSummoningEvent, 30000);
    }

    private void StartSummoningEvent()
    {
        GetOwner().ClearQueuedSkills();
        GetOwner().QueueSkill(21646, 1, 10000);
    }

    public override void OnEndUseSkill(SkillTemplate st, int skillLevel)
    {
        switch (st.GetSkillId())
        {
            case 21533: // Ice Explosion
                lastSpawnEvent = 1;
                task = ThreadPoolManager.GetInstance().Schedule(_ => { StartSummoningEvent(); return ValueTask.CompletedTask; }, 30000L);
                break;
            case 21637: // Rigid Gait
                ThreadPoolManager.GetInstance().Schedule(_ => { AttackIcingCrystal(); return ValueTask.CompletedTask; }, 4500L);
                break;
            case 21646: // Summon Freezing Crystal
                if (lastSpawnEvent == 1)
                {
                    SpawnIcingCrystals();
                    task = ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        GetOwner().GetEffectController().SetAbnormal(AbnormalState.SANCTUARY);
                        AttackIcingCrystal();
                        return ValueTask.CompletedTask;
                    }, 10000L);
                }
                else
                {
                    SpawnFrigidCrystals();
                    task = ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        GetOwner().ClearQueuedSkills();
                        GetOwner().QueueSkill(21632, 1, 0); // Frigid Blast
                        GetOwner().QueueSkill(21633, 1, 8000); // Ice Explosion
                        return ValueTask.CompletedTask;
                    }, 10000L);
                }
                break;
        }
    }

    private void AttackIcingCrystal()
    {
        VisibleObject servant = GetKnownList().FindObject(o => o.Get() is Npc npc && npc.GetNpcId() == 855700 && !npc.IsDead() && !npc.GetLifeStats().IsAboutToDie());
        if (servant != null)
        {
            GetOwner().SetTarget(servant);
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21637, 67, servant).UseSkill();
        }
        else
        {
            GetOwner().GetEffectController().UnsetAbnormal(AbnormalState.SANCTUARY);
            lastSpawnEvent = 2;
            task = ThreadPoolManager.GetInstance().Schedule(_ => { StartSummoningEvent(); return ValueTask.CompletedTask; }, 30000L);
        }
    }

    private void SpawnIcingCrystals()
    {
        Spawn(855608, 822.0f, 567.7f, 1701.045f, (sbyte)0);
        Spawn(855608, 811.6f, 577.7f, 1701.045f, (sbyte)0);
        Spawn(855608, 811.7f, 556.5f, 1701.045f, (sbyte)0);
        Spawn(855608, 801.1f, 567.9f, 1701.045f, (sbyte)0);
    }

    private void SpawnFrigidCrystals()
    {
        Spawn(855607, 790.7f, 567.7f, 1701.045f, (sbyte)0);
        Spawn(855607, 797.0f, 552.0f, 1701.045f, (sbyte)0);
        Spawn(855607, 797.1f, 583.1f, 1701.045f, (sbyte)0);
        Spawn(855607, 801.1f, 567.9f, 1701.045f, (sbyte)0);
        Spawn(855607, 802.5f, 557.9f, 1701.045f, (sbyte)0);
        Spawn(855607, 802.7f, 576.6f, 1701.045f, (sbyte)0);
        Spawn(855607, 811.6f, 577.8f, 1701.045f, (sbyte)0);
        Spawn(855607, 811.6f, 587.7f, 1701.045f, (sbyte)0);
        Spawn(855607, 811.7f, 556.5f, 1701.045f, (sbyte)0);
        Spawn(855607, 811.8f, 567.9f, 1701.045f, (sbyte)0);
        Spawn(855607, 812.0f, 545.5f, 1701.045f, (sbyte)0);
        Spawn(855607, 820.3f, 557.9f, 1701.045f, (sbyte)0);
        Spawn(855607, 820.5f, 576.4f, 1701.045f, (sbyte)0);
        Spawn(855607, 822.0f, 567.7f, 1701.045f, (sbyte)0);
        Spawn(855607, 826.1f, 551.2f, 1701.045f, (sbyte)0);
        Spawn(855607, 825.9f, 582.1f, 1701.045f, (sbyte)0);
        Spawn(855607, 832.8f, 567.6f, 1701.045f, (sbyte)0);
    }

    private void CancelTask()
    {
        if (task != null && !task.IsDone())
        {
            task.Cancel(true);
        }
    }

    protected override void HandleBackHome()
    {
        isActive.Set(false);
        CancelTask();
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        CancelTask();
        base.HandleDespawned();
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }
}
