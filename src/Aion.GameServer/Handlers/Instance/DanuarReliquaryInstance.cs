using System.Linq;
using System.Threading;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/DanuarReliquaryInstance (Ritsu, Estrayl, Yeats) : GeneralInstanceHandler. @InstanceID(301110000); protected getExitId/getTreasureBoxId/getEnragedModorId/getCursedModorId/getRealCloneId/getFakeCloneId overridable; AtomicBoolean/AtomicInteger→int+Interlocked; ScheduledFuture wipeTask→ScheduledTask (cancel(false)→Cancel(false), isCancelled()→IsCancelled); stream().allMatch(Creature::isDead)→.All(IsDead); forEachNpc; onDie/spawnClones/onInstanceEnd/scheduleWipe/onPlayerLogout/onInstanceDestroy/onBackHome 1:1.</summary>
[InstanceID(301110000)]
public class DanuarReliquaryInstance : GeneralInstanceHandler
{
    private int isCursedModorActive;
    private int cloneKills;
    private ScheduledTask wipeTask;

    public DanuarReliquaryInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected virtual int GetExitId()
    {
        return 730843;
    }

    protected virtual int GetTreasureBoxId()
    {
        return 701795;
    }

    protected virtual int GetEnragedModorId()
    {
        return 231305;
    }

    protected virtual int GetCursedModorId()
    {
        return 231304;
    }

    protected virtual int GetRealCloneId()
    {
        return 284383; // 855244
    }

    protected virtual int GetFakeCloneId()
    {
        return 284384; // 855244
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        int npcId = npc.GetNpcId();
        switch (npcId)
        {
            case 284377: // Idean Obscura
            case 284378: // Idean Lapilima
            case 284379: // Danuar Reliquary Novun
                npc.GetController().Delete();
                if (instance.GetNpcs(284377, 284378, 284379).All(c => c.IsDead()) && Interlocked.CompareExchange(ref isCursedModorActive, 1, 0) == 0)
                {
                    Spawn(GetCursedModorId(), 256.62f, 257.79f, 241.79f, (byte)90);
                    Npc cursedModor = GetNpc(GetCursedModorId());
                    if (cursedModor != null)
                    {
                        // SkillEngine.getInstance().getSkill(cursedModor, 21168, 1, cursedModor).useWithoutPropSkill();
                        // sendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5_INDER_RUNE_START());
                        ScheduleWipe();
                    }
                }
                break;
            case 284383: // Modor's Clone
            case 855244: // Modor's Clone
                DeleteAliveNpcs(GetFakeCloneId());
                npc.GetController().Delete();
                ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    if (Interlocked.Increment(ref cloneKills) >= 3)
                    {
                        Spawn(GetEnragedModorId(), 256.62f, 257.79f, 241.79f, (byte)90);
                    }
                    else
                    {
                        SpawnClones();
                    }
                }, 2000L);
                break;
            case 231305: // Enraged Queen Modor
            case 234691: // Crazed Modor
                OnInstanceEnd(true);
                break;
            case 701795: // Treasure Box
            case 802183:
                break;
            default:
                npc.GetController().Delete();
                break;
        }
    }

    private void SpawnClones()
    {
        int spawnCase = Rnd.Get(1, 5);
        switch (spawnCase)
        {
            case 1:
                Spawn(GetRealCloneId(), 255.5489f, 293.42154f, 253.78925f, (byte)90);
                break;
            case 2:
                Spawn(GetRealCloneId(), 232.5363f, 263.90112f, 248.65384f, (byte)114);
                break;
            case 3:
                Spawn(GetRealCloneId(), 240.11194f, 235.08876f, 251.14906f, (byte)17);
                break;
            case 4:
                Spawn(GetRealCloneId(), 271.23627f, 230.30913f, 250.92981f, (byte)42);
                break;
            case 5:
                Spawn(GetRealCloneId(), 284.6919f, 262.7201f, 248.75252f, (byte)63);
                break;
        }

        if (spawnCase != 1)
            Spawn(GetFakeCloneId(), 255.5489f, 293.42154f, 253.78925f, (byte)90);
        if (spawnCase != 2)
            Spawn(GetFakeCloneId(), 232.5363f, 263.90112f, 248.65384f, (byte)114);
        if (spawnCase != 3)
            Spawn(GetFakeCloneId(), 240.11194f, 235.08876f, 251.14906f, (byte)17);
        if (spawnCase != 4)
            Spawn(GetFakeCloneId(), 271.23627f, 230.30913f, 250.92981f, (byte)42);
        if (spawnCase != 5)
            Spawn(GetFakeCloneId(), 284.6919f, 262.7201f, 248.75252f, (byte)63);
    }

    protected virtual void OnInstanceEnd(bool successful)
    {
        CancelWipeTask();
        Npc modor = GetNpc(GetEnragedModorId());
        if (modor == null)
        {
            modor = GetNpc(GetCursedModorId());
        }
        if (modor != null)
        {
            if (successful)
            {
                PacketSendUtility.BroadcastMessage(modor, 343629);
            }
            else
            {
                PacketSendUtility.BroadcastMessage(modor, 1500739);
            }
        }
        instance.ForEachNpc(npc => npc.GetController().Delete());
        Spawn(GetExitId(), 255.66669f, 263.78525f, 241.7986f, (byte)86); // Spawn exit portal
        if (successful)
            Spawn(GetTreasureBoxId(), 256.65f, 258.09f, 241.78f, (byte)100); // Treasure Box
    }

    private void ScheduleWipe()
    {
        wipeTask = ThreadPoolManager.GetInstance().Schedule(() =>
        {
            Spawn(284386, 256.60f, 257.99f, 241.78f, (byte)0);
            OnInstanceEnd(false);
        }, 15 * 60000L);
    }

    protected void CancelWipeTask()
    {
        if (wipeTask != null && !wipeTask.IsCancelled)
            wipeTask.Cancel(false);
    }

    public override void OnPlayerLogout(Player player)
    {
        base.OnPlayerLogout(player);
        if (player.IsDead())
            TeleportService.MoveToBindLocation(player);
    }

    public override void OnInstanceDestroy()
    {
        CancelWipeTask();
    }

    public override void OnBackHome(Npc npc)
    {
        if (npc.GetNpcId() == GetEnragedModorId() || npc.GetNpcId() == GetCursedModorId())
        {
            instance.ForEachNpc(other =>
            {
                if (other.GetNpcId() != npc.GetNpcId())
                {
                    other.GetController().Delete();
                }
            });
        }
    }
}
