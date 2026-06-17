using System;
using System.Collections.Generic;
using System.Threading;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/InfinityShardInstance (Cheatkiller, Luzien, Estrayl) : GeneralInstanceHandler. @InstanceID(300800000). List&lt;Future&gt;→List&lt;ScheduledTask&gt;; AtomicBoolean isRunning→int+Interlocked. onDie generator/resonator webs; onBackHome/onStartEffect; checkGeneratorState charge sequence; fail timer; isBoss override (Hyperion). 1:1.</summary>
[InstanceID(300800000)]
public class InfinityShardInstance : GeneralInstanceHandler
{
    private readonly List<ScheduledTask> tasks = new();
    private int isRunning = 1;

    public InfinityShardInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 231083: // Hyperion Defense Elite Assassin
                RemoveIdeAethericFieldEffect(231074); // Ide Forcefield Generator
                break;
            case 231087: // Hyperion Defense Elite Magus
                RemoveIdeAethericFieldEffect(231078); // Ide Forcefield Generator
                break;
            case 231079: // Hyperion Defense Elite Assassin
                RemoveIdeAethericFieldEffect(231082); // Ide Forcefield Generator
                break;
            case 231075: // Agent Kabalash
                RemoveIdeAethericFieldEffect(231086); // Ide Forcefield Generator
                break;
            case 231074:
            case 231078:
            case 231082:
            case 231086:
                npc.GetController().Delete();
                CheckGeneratorState();
                break;
            case 231092:
            case 231093:
            case 231094:
            case 231095:
            case 231102:
                tasks.Add(
                    ThreadPoolManager.GetInstance().Schedule(() => Spawn(npc.GetNpcId(), npc.GetX(), npc.GetY(), npc.GetZ(), (byte)0), Rnd.Get(29000, 31000)));
                break;
            case 231073: // Hyperion
                CancelTasks();
                DespawnAllAndSpawnExit();
                // rewardGP();
                break;
        }
    }

    private void RemoveIdeAethericFieldEffect(int npcId)
    {
        Npc npc = instance.GetNpc(npcId);
        if (npc != null)
            npc.GetEffectController().RemoveEffect(21371);
    }

    public override void OnBackHome(Npc npc)
    {
        if (npc.GetNpcId() == 231073) // hyperion
        {
            if (Interlocked.Exchange(ref isRunning, 0) == 1)
            {
                CancelTasks();
                DespawnAllAndSpawnExit();
            }
        }
    }

    public override void OnStartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        if (effected is Npc && ((Npc)effected).GetNpcId() == 231073)
            switch (effect.GetSkillId())
            {
                case 21258: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDRUNEWP_CHARGER1_COMPLETED()); break;
                case 21382: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDRUNEWP_CHARGER2_COMPLETED()); break;
                case 21384: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDRUNEWP_CHARGER3_COMPLETED()); break;
                case 21416:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDRUNEWP_CHARGER4_COMPLETED());
                    ThreadPoolManager.GetInstance().Schedule(() => FailInstance(true), 12000L);
                    break;
            }
    }

    private void CheckGeneratorState()
    {
        for (int id = 231074; id <= 231086; id += 4)
        {
            if (instance.GetNpc(id) != null)
            {
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDRUNEWP_BROKENPROTECTION());
                return;
            }
        }
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDRUNEWP_BROKENPROTECTIONALL());
        DeleteAliveNpcs(730741); // remove barrier in center
        instance.GetNpc(231073).GetEffectController().RemoveEffect(21254);
        StartHatingNearestPlayer();
        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(instance.GetNpc(231073), 21255, 56, instance.GetNpc(231073)).UseWithoutPropSkill();
        SpawnResonators();
        SpawnTurrets();
        StartFailTimer();
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDRUNEWP_CHARGING());
    }

    private void StartHatingNearestPlayer()
    {
        Npc hyperion = instance.GetNpc(231073);
        Player nearest = null;
        double dist = double.MaxValue;
        foreach (Player p in instance.GetPlayersInside())
        {
            if (!p.IsDead())
            {
                double locDist = PositionUtil.GetDistance(hyperion, p, false);
                if (locDist < dist)
                {
                    dist = locDist;
                    nearest = p;
                }
            }
        }
        if (nearest != null)
        {
            hyperion.GetAi().OnCreatureEvent(AiEventType.CREATURE_AGGRO, nearest);
        }
    }

    private void StartFailTimer()
    {
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            Npc hyperion = instance.GetNpc(231073);
            if (hyperion != null && !hyperion.IsDead())
            {
                FailInstance(false);
            }
        }, 20 * 60 * 1000L)); // 20 min
    }

    private void FailInstance(bool wipePlayers)
    {
        if (Interlocked.Exchange(ref isRunning, 0) == 1)
        {
            CancelTasks();
            DeleteAliveNpcs(231073);
            SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDRUNEWP_USER_KILL());
            if (wipePlayers)
                foreach (Npc npc in instance.GetNpcs(231104))
                    npc.GetController().UseSkill(21199);
            ThreadPoolManager.GetInstance().Schedule(DespawnAllAndSpawnExit, 1500L); // delay despawn for invisible NPCs to use the skill
        }
    }

    private void SpawnResonators()
    {
        Spawn(231092, 108.55013f, 138.96948f, 132.60164f, (byte)0);
        ThreadPoolManager.GetInstance().Schedule(() => Spawn(231093, 126.5471f, 154.47961f, 131.47116f, (byte)0), 10000L);
        ThreadPoolManager.GetInstance().Schedule(() => Spawn(231094, 146.72455f, 139.12267f, 132.68515f, (byte)0), 20000L);
        ThreadPoolManager.GetInstance().Schedule(() => Spawn(231095, 129.41306f, 121.34766f, 131.47116f, (byte)0), 30000L);
    }

    private void SpawnTurrets()
    {
        ThreadPoolManager.GetInstance().Schedule(() => Spawn(231102, 107.53553f, 142.51953f, 127.03997f, (byte)0), Rnd.Get(30000, 45000));
        ThreadPoolManager.GetInstance().Schedule(() => Spawn(231102, 113.86417f, 154.06656f, 127.68255f, (byte)110), Rnd.Get(30000, 45000));
        ThreadPoolManager.GetInstance().Schedule(() => Spawn(231102, 144.52719f, 122.26577f, 127.44639f, (byte)45), Rnd.Get(30000, 45000));
        ThreadPoolManager.GetInstance().Schedule(() => Spawn(231102, 150.33377f, 132.67754f, 126.57981f, (byte)50), Rnd.Get(30000, 45000));
    }

    private void DespawnAllAndSpawnExit()
    {
        instance.ForEachNpc(npc => npc.GetController().DeleteIfAliveOrCancelRespawn());
        Spawn(730842, 146.65f, 135.33f, 112.18f, (byte)59); // exit portal
    }

    /*
     * private void rewardGP() {
     * int reward = 600 / instance.getPlayersInside().size();
     * for (Player p : instance.getPlayersInside()) {
     * if (p != null && p.isOnline())
     * GloryPointsService.addGp(p, reward, true);
     * }
     * }
     */

    private void CancelTasks()
    {
        foreach (ScheduledTask task in tasks)
            if (task != null && !task.IsCancelled)
                task.Cancel(true);
    }

    public override void OnPlayerLogout(Player player)
    {
        base.OnPlayerLogout(player);
        if (player.IsDead())
        {
            TeleportService.MoveToBindLocation(player);
        }
    }

    public override void OnInstanceDestroy()
    {
        CancelTasks();
    }

    public override bool IsBoss(Npc npc)
    {
        return npc.GetNpcId() == 231073; // Hyperion
    }
}
