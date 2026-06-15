using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/padmarashkasCave/PadmarashkaAI (Ritsu, Luzien).</summary>
[AIName("padmarashka")]
public class PadmarashkaCaveAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(95, 50, 25);
    private bool isStart = false;
    private bool canThinkFlag = false;
    private ScheduledTask mainSkillTask;

    public PadmarashkaCaveAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        PutToSleep();
    }

    protected override void HandleAttack(Creature creature)
    {
        if (!this.GetEffectController().HasAbnormalEffect(19186) && !isStart)
        {
            WakeUp();
        }
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        if (!this.GetEffectController().HasAbnormalEffect(19186) && !isStart)
        {
            WakeUp();
        }
        base.HandleCreatureAggro(creature);
    }

    private void WakeUp()
    {
        canThinkFlag = true;
        isStart = true;
        ScheduleSpawnEntrance();
        StartMainSkillTask();
    }

    private void StartMainSkillTask()
    {
        mainSkillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), Rnd.NextInt(2) == 0 ? 20401 : 20093, 48, GetTarget()).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(10000), System.TimeSpan.FromMilliseconds(20000));
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 95:
                Stage1();
                break;
            case 50:
                Stage2();
                break;
            case 25:
                Stage3();
                break;
        }
    }

    private void ScheduleSpawnEntrance()
    {
        int delay = 60000;
        if (IsDead() || !isStart)
            return;
        else
        {
            GetOwner().ClearAttackedCount();
            int count = Rnd.Get(1, 3);
            for (int i = 0; i < count; i++)
            {
                int npcId = 282792 + Rnd.NextInt(5);
                AttackPlayer((Npc)Spawn(npcId, 531.465f + Rnd.Get(-5, 5), 316.921f + Rnd.Get(-5, 5), 65.351f, (sbyte)105));
            }
            ScheduleDelayEntrance(delay);
        }
    }

    private void AttackPlayer(Npc npc)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            npc.SetTarget(GetTarget());
            npc.GetAi().SetStateIfNot(AIState.WALKING);
            npc.SetState(CreatureState.ACTIVE, true);
            npc.GetMoveController().MoveToTargetObject();
            PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
            return ValueTask.CompletedTask;
        }, 1000L);
    }

    private void Stage1()
    {
        int delay = 60000;
        if (IsDead() || !isStart)
            return;
        else
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19177, 55, GetOwner()).UseNoAnimationSkill();
            switch (Rnd.Get(1, 10))
            {
                case 1:
                    Spawn(282613, 575.064f, 163.573f, 66.01f, (sbyte)0);
                    break;
                case 2:
                    Spawn(282613, 587.361f, 165.187f, 66.01f, (sbyte)0);
                    break;
                case 3:
                    Spawn(282613, 577.463f, 155.553f, 66.01f, (sbyte)0);
                    break;
                case 4:
                    Spawn(282613, 582.064f, 160.785f, 66.01f, (sbyte)0);
                    break;
                case 5:
                    Spawn(282613, 586.160f, 157.214f, 66.01f, (sbyte)0);
                    break;
                case 6:
                    Spawn(282613, 571.436f, 150.558f, 66.01f, (sbyte)0);
                    break;
                case 7:
                    Spawn(282613, 577.574f, 150.415f, 66.01f, (sbyte)0);
                    break;
                case 8:
                    Spawn(282613, 581.354f, 151.640f, 66.01f, (sbyte)0);
                    break;
                case 9:
                    Spawn(282613, 589.382f, 149.758f, 66.01f, (sbyte)0);
                    break;
                case 10:
                    Spawn(282613, 583.469f, 145.809f, 66.01f, (sbyte)0);
                    break;
            }
            ScheduleDelayStage1(delay);
            if (GetPosition().GetWorldMapInstance().GetNpc(218675) == null)
            {
                Spawn(218675, 573.413f, 179.518f, 66.220f, (sbyte)30);
            }
            else if (GetPosition().GetWorldMapInstance().GetNpc(218676) == null)
            {
                Spawn(218676, 581.827f, 179.587f, 66.250f, (sbyte)30);
            }
        }
    }

    private void Stage2()
    {
        int delay = 120000;
        if (IsDead() || !isStart)
            return;
        else
        {
            PacketSendUtility.BroadcastToMap(GetOwner(), 1401214); // Huge egg is revealed
            switch (Rnd.Get(1, 4))
            {
                case 1:
                    Spawn(282614, 510.12497f, 250.17401f, 66.625f, (sbyte)0);
                    break;
                case 2:
                    Spawn(282614, 530.1312f, 170.20688f, 65.875f, (sbyte)0);
                    break;
                case 3:
                    Spawn(282614, 610.32495f, 210.19055f, 66.125f, (sbyte)0);
                    break;
                case 4:
                    Spawn(282614, 590.1816f, 290.31775f, 66.75f, (sbyte)0);
                    break;
            }
            ScheduleDelayStage2(delay);
        }
    }

    private void Stage3()
    { // Spawn Rock
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19179, 55, GetOwner()).UseNoAnimationSkill();
        PacketSendUtility.BroadcastToMap(GetOwner(), 1401215); // Cave crumble
        Spawn(282140, 520.99585f, 270.23776f, 66.25f, (sbyte)0);
        Spawn(282140, 506.8137f, 238.60612f, 66.57414f, (sbyte)0);
        Spawn(282140, 495.94504f, 218.84671f, 67.58238f, (sbyte)0);
        Spawn(282140, 502.39307f, 196.61835f, 67.5513f, (sbyte)0);
        Spawn(282140, 510.88928f, 178.31491f, 66.869156f, (sbyte)0);
        Spawn(282140, 530.8308f, 160.27686f, 65.926926f, (sbyte)0);
        Spawn(282140, 551.6416f, 145.72856f, 67.93133f, (sbyte)0);
        Spawn(282140, 567.2481f, 162.51135f, 66.09399f, (sbyte)0);
        Spawn(282140, 585.17523f, 188.57863f, 66.125f, (sbyte)0);
        Spawn(282140, 611.51733f, 186.78618f, 66.25f, (sbyte)0);
        Spawn(282140, 624.74023f, 205.0654f, 66.255615f, (sbyte)0);
        Spawn(282140, 627.2288f, 228.05437f, 66.3268f, (sbyte)0);
        Spawn(282140, 619.18396f, 258.662f, 69.37874f, (sbyte)0);
        Spawn(282140, 604.1967f, 284.47342f, 66.71312f, (sbyte)0);
        Spawn(282140, 589.62775f, 293.01245f, 66.75f, (sbyte)0);
        Spawn(282140, 599.13367f, 264.5496f, 66.25f, (sbyte)0);
        Spawn(282140, 517.9813f, 220.19554f, 66.86278f, (sbyte)0);
        Spawn(282140, 533.5666f, 194.63768f, 66.125f, (sbyte)0);
    }

    private void ScheduleDelayStage2(int delay)
    {
        if (!isStart && !IsDead())
            return;
        else
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                Stage2();
                return ValueTask.CompletedTask;
            }, (long)delay);
        }
    }

    private void ScheduleDelayStage1(int delay)
    {
        if (!isStart && !IsDead())
            return;
        else
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                Stage1();
                return ValueTask.CompletedTask;
            }, (long)delay);
        }
    }

    private void ScheduleDelayEntrance(int delay)
    {
        if (!isStart && !IsDead())
            return;
        else
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                ScheduleSpawnEntrance();
                return ValueTask.CompletedTask;
            }, (long)delay);
        }
    }

    private void PutToSleep()
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19186, 55, GetOwner()).UseNoAnimationSkill();
        this.GetEffectController().SetAbnormal(AbnormalState.SLEEP);
        canThinkFlag = false;
    }

    private void SpawnPadmaProtector()
    {
        Spawn(218670, 533.107f, 247.685f, 66.761f, (sbyte)45);
        Spawn(218671, 581.092f, 265.049f, 66.843f, (sbyte)15);
        Spawn(218673, 547.838f, 264.624f, 66.875f, (sbyte)45);
        Spawn(218674, 595.748f, 248.045f, 66.250f, (sbyte)15);
    }

    private void DeSpawnNpcs()
    {
        // Despawn Stage 3
        DespawnNpcs(282140);
        // Despawn Stage 2
        DespawnNpcs(282614);
        DespawnNpcs(282712);
        DespawnNpcs(282620);
        // Despawn Stage 1
        DespawnNpcs(282613);
        DespawnNpcs(218675);
        DespawnNpcs(218676);
        DespawnNpcs(282715);
        DespawnNpcs(282716);
        // Despawn Entrance mobs
        DespawnNpcs(282792);
        DespawnNpcs(282793);
        DespawnNpcs(282794);
        DespawnNpcs(282795);
        DespawnNpcs(282796);
        // Despawn Padma Protector
        DespawnNpcs(218670);
        DespawnNpcs(218671);
        DespawnNpcs(218673);
        DespawnNpcs(218674);
    }

    private void DespawnNpcs(int npcId)
    {
        List<Npc> npcs = GetPosition().GetWorldMapInstance().GetNpcs(npcId);
        foreach (Npc npc in npcs)
        {
            if (npc != null)
            {
                npc.GetController().Delete();
            }
        }
    }

    private void CancelTask()
    {
        if (mainSkillTask != null && !mainSkillTask.IsDone())
            mainSkillTask.Cancel(true);
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        isStart = false;
        hpPhases.Reset();
        CancelTask();
        DeSpawnNpcs();
        PutToSleep();
        SpawnPadmaProtector();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
        isStart = false;
        DeSpawnNpcs();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
        DeSpawnNpcs();
    }

    public override bool CanThink()
    {
        return canThinkFlag;
    }
}
