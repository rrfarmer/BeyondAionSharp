using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/BrigadeGeneralTahabataAI (@author Cheatkiller, Luzien).</summary>
[AIName("brigadegeneraltahabata")]
public class BrigadeGeneralTahabataAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(96, 75, 60, 55, 40, 25, 20, 10, 7);
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? piercingStrikeTask;
    private ScheduledTask? fireStormTask;

    public BrigadeGeneralTahabataAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
            GetPosition().GetWorldMapInstance().SetDoorState(610, false);
        hpPhases.TryEnterNextPhase(this);
    }

    private void StartPiercingStrikeTask()
    {
        piercingStrikeTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (!IsDead())
                StartPiercingStrikeEvent();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(15000), System.TimeSpan.FromMilliseconds(20000));
    }

    private void StartFireStormTask()
    {
        fireStormTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (!IsDead())
                StartFireStormEvent();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(10000), System.TimeSpan.FromMilliseconds(20000));
    }

    private void StartFireStormEvent()
    {
        if (GetPosition().GetWorldMapInstance().GetNpc(283045) == null)
        {
            AIActions.UseSkill(this, 20758);
            RndSpawn(283045, Rnd.Get(1, 4));
        }
    }

    private void StartPiercingStrikeEvent()
    {
        TeleportRandomPlayer();
        Skill skill = SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20754, 60, GetOwner());
        if (skill != null)
        {
            skill.UseNoAnimationSkill();
            if (skill.GetEffectedList().Count != 0)
                UseMultiSkill();
        }
    }

    private void UseMultiSkill()
    {
        ThreadPoolManager.GetInstance().Schedule(_ => { AIActions.UseSkill(this, 20755); return ValueTask.CompletedTask; }, 3000L);
    }

    private void TeleportRandomPlayer()
    {
        Creature target = GetAggroList().GetTarget(AggroTarget.RANDOM, 40);
        if (target == null)
            return;
        GetOwner().SetTarget(target);
        World.World.GetInstance().UpdatePosition(GetOwner(), target.GetX(), target.GetY(), target.GetZ(), (byte)0);
        PacketSendUtility.BroadcastPacketAndReceive(GetOwner(), new SM_FORCED_MOVE(GetOwner(), GetOwner()));
    }

    private void CancelPiercingStrike()
    {
        if (piercingStrikeTask != null && !piercingStrikeTask.IsCancelled)
        {
            piercingStrikeTask.Cancel(true);
        }
    }

    private void CancelFireStorm()
    {
        if (fireStormTask != null && !fireStormTask.IsCancelled)
        {
            fireStormTask.Cancel(true);
        }
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 96:
                LavaEruptionEvent(283116); // 4.0
                StartPiercingStrikeTask();
                break;
            case 75:
                AIActions.UseSkill(this, 20761);
                StartFireStormTask();
                break;
            case 60:
                AIActions.UseSkill(this, 20761);
                break;
            case 55:
                LavaEruptionEvent(283118); // 4.0
                break;
            case 40:
            case 25:
                AIActions.UseSkill(this, 20761);
                break;
            case 20:
                CancelFireStorm();
                CancelPiercingStrike();
                LavaEruptionEvent(283120); // 4.0
                break;
            case 10:
                AIActions.UseSkill(this, 20942);
                break;
            case 7:
                AIActions.UseSkill(this, 20883);
                Spawn(283102, 679.88f, 1068.88f, 497.88f, (sbyte)0); // 4.0
                break;
        }
    }

    private void LavaEruptionEvent(int floorId)
    {
        AIActions.TargetSelf(this);
        AIActions.UseSkill(this, 20756);
        if (GetPosition().GetWorldMapInstance().GetNpc(283051) == null) // 4.0
        {
            Spawn(283051, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // 4.0
        }
        Spawnfloor(floorId);
    }

    private void Spawnfloor(int floor)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Spawn(floor, 679.88f, 1068.88f, 497.88f, (sbyte)0);
            Spawn(floor + 1, 679.88f, 1068.88f, 497.88f, (sbyte)0);
            return ValueTask.CompletedTask;
        }, 10000L);
    }

    private void RndSpawn(int npcId, int count)
    {
        for (int i = 0; i < count; i++)
            RndSpawnInRange(npcId, 10);
    }

    private void DeleteAdds()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(283116, 283117, 283118, 283119, 283120, 283121, 283102))
            npc.GetController().Delete();
    }

    protected override void HandleBackHome()
    {
        isHome.Set(true);
        GetPosition().GetWorldMapInstance().SetDoorState(610, true);
        base.HandleBackHome();
        hpPhases.Reset();
        GetEffectController().RemoveEffect(20942);
        DeleteAdds();
        CancelPiercingStrike();
        CancelFireStorm();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelPiercingStrike();
        CancelFireStorm();
    }

    protected override void HandleDied()
    {
        GetPosition().GetWorldMapInstance().SetDoorState(610, true);
        base.HandleDied();
        DeleteAdds();
        CancelPiercingStrike();
        CancelFireStorm();
    }
}
