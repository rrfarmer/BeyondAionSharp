using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/VanktristAI (@author w4terbomb).</summary>
[AIName("vanktrist")]
public class VanktristAI : CrucibleBossAI
{
    private const int GLOWING_VORTEX = 282438;
    private const int UMBRAL_VORTEX = 282439;
    private const int BLACK_HOLE = 282374;
    private const int LESSER_BLACK_HOLE = 282415;

    private readonly List<Npc> summons = new List<Npc>();
    private ScheduledTask? blackHoleTask, waveTask;
    private bool umbralWave = true;
    private int strikeIn, shieldIn, deathShieldIn;

    public VanktristAI(Npc owner) : base(owner)
    {
    }

    protected override void OnFightStart()
    {
        blackHoleTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            BlackHole();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(40000), TimeSpan.FromMilliseconds(60000));
        strikeIn = 1;
        shieldIn = 0;
        deathShieldIn = 60;
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        foreach (int phaseHpPercent in new int[] { 80, 50, 20 })
            if (Passed(phaseHpPercent))
                HandleHpPhase(phaseHpPercent);
        if (hp > 50)
        {
            if (--strikeIn == 0)
            {
                Cast(19564, 10);
                shieldIn = 15;
                strikeIn = 35;
            }
            if (shieldIn > 0 && --shieldIn == 0)
                Cast(19566, 10);
        }
        else if (--deathShieldIn <= 0)
        {
            Cast(19569, 10);
            deathShieldIn = 60;
        }
    }

    private void HandleHpPhase(int phaseHpPercent)
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500233);
        GetOwner().QueueSkill(19567, 10);
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500235);
        GetOwner().QueueSkill(19568, 10);
        SwitchToLowestHpTarget();
        switch (phaseHpPercent)
        {
            case 50:
                SpawnSummons(GLOWING_VORTEX, 3);
                CancelTask(blackHoleTask);
                blackHoleTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
                {
                    BlackHole();
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(15000), TimeSpan.FromMilliseconds(30000));
                ScheduleWave(40000);
                break;
            case 20:
                SpawnSummons(GLOWING_VORTEX, 2);
                SpawnSummons(UMBRAL_VORTEX, 2);
                break;
        }
    }

    private void BlackHole()
    {
        if (IsDead())
            return;
        Player target = GetRandomTarget();
        if (target == null)
            return;
        if (GetLifeStats().GetHpPercentage() > 50)
        {
            GetOwner().QueueSkill(19565, 10, 0, NpcSkillTargetAttribute.RANDOM);
            SpawnTemporary(LESSER_BLACK_HOLE, target, 5000);
        }
        else
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1500234);
            GetOwner().QueueSkill(19566, 10, 0, NpcSkillTargetAttribute.RANDOM);
            SpawnTemporary(BLACK_HOLE, target, 10000);
        }
    }

    private void ScheduleWave(int delay)
    {
        waveTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (IsDead())
                return ValueTask.CompletedTask;
            int hp = GetLifeStats().GetHpPercentage();
            if (hp > 50 && hp <= 60)
            {
                SpawnSummons(BLACK_HOLE, 1);
                Player target = GetRandomTarget();
                if (target != null)
                    SpawnTemporary(LESSER_BLACK_HOLE, target, 10000);
            }
            else if (hp > 20)
            {
                SpawnSummons(umbralWave ? UMBRAL_VORTEX : GLOWING_VORTEX, 3);
            }
            else if (umbralWave)
            {
                SpawnSummons(UMBRAL_VORTEX, 2);
                SpawnSummons(GLOWING_VORTEX, 2);
            }
            else
            {
                SpawnSummons(UMBRAL_VORTEX, 4);
            }
            umbralWave = !umbralWave;
            ScheduleWave(hp > 20 ? 40000 : 35000);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(delay));
    }

    private void SpawnSummons(int npcId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (RndSpawnInRange(npcId, 1, 5) is Npc npc)
                AddSummon(npc);
        }
    }

    private void SpawnTemporary(int npcId, Player target, int lifeTime)
    {
        if (Spawn(npcId, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0) is Npc npc)
        {
            AddSummon(npc);
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                npc.GetController().Delete();
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(lifeTime));
        }
    }

    private void AddSummon(Npc npc)
    {
        lock (summons)
        {
            summons.Add(npc);
        }
    }

    private void SwitchToLowestHpTarget()
    {
        Player? lowest = GetAttackablePlayers().OrderBy(p => p.GetLifeStats().GetHpPercentage()).FirstOrDefault();
        if (lowest != null)
            GetAggroList().AddHate(lowest, 10000);
    }

    private Player GetRandomTarget()
    {
        return Rnd.Get(GetAttackablePlayers());
    }

    private List<Player> GetAttackablePlayers()
    {
        List<Player> players = new List<Player>();
        GetKnownList().ForEachPlayer(player =>
        {
            if (!player.IsDead() && GetAggroList().IsHating(player) && GeoService.GetInstance().CanSee(GetOwner(), player))
                players.Add(player);
        });
        return players;
    }

    private static void CancelTask(ScheduledTask? task)
    {
        if (task != null && !task.IsDone())
            task.Cancel(true);
    }

    protected override void OnFightEnd()
    {
        CancelTask(blackHoleTask);
        CancelTask(waveTask);
        blackHoleTask = null;
        waveTask = null;
        umbralWave = true;
        lock (summons)
        {
            foreach (Npc npc in summons)
                npc.GetController().Delete();
            summons.Clear();
        }
    }

    protected override void HandleNotAtHome()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500236);
        base.HandleNotAtHome();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500232);
    }

    protected override void HandleDied()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500237);
        base.HandleDied();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.ALLOW_DECAY => false,
            _ => base.Ask(question),
        };
    }
}
