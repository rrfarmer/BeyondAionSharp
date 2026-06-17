using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/DanuarMysticariumInstance (Yeats) : GeneralInstanceHandler. @InstanceID(300480000); List&lt;Future&lt;?&gt;&gt;→List&lt;ScheduledTask&gt; (cancel(true)→Cancel(true), isCancelled()→IsCancelled); onOpenDoor arrow-switch; handleUseItemFinish start tasks+teleport; startTasks scheduled msgs; leaveInstance. 1:1 (TODO Mini Game 2&amp;3 preserved).</summary>
[InstanceID(300480000)]
public class DanuarMysticariumInstance : GeneralInstanceHandler
{
    private List<ScheduledTask> tasks;

    public DanuarMysticariumInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnOpenDoor(int door)
    {
        switch (door)
        {
            case 6:
                Spawn(219964, 225.53f, 529.7f, 153.04f, (byte)100);
                break;
            case 7:
                Spawn(219963, 241.602f, 541.79f, 152.591f, (byte)95);
                break;
            case 8:
                Spawn(219965, 262.17f, 545.68f, 150.51f, (byte)85);
                break;
            case 10:
                Spawn(219964, 295.04f, 547.48f, 148.73f, (byte)90);
                break;
            case 11:
                Spawn(219963, 317.654f, 545.801f, 148.8f, (byte)80);
                break;
            case 12:
                Spawn(219965, 336.94f, 532.89f, 148.472f, (byte)75);
                break;
            case 13:
                Spawn(219969, 348.14f, 512.56f, 148.19f, (byte)65);
                break;
            case 101:
                Spawn(219963, 212.068f, 510.02f, 153.23f, (byte)115);
                break;
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 731583:
                StartTasks();
                instance.SetDoorState(3, true);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5Re_solo_game1_1());
                TeleportService.TeleportTo(player, instance, 140.45f, 182.2f, 242f, (byte)10, TeleportAnimation.FADE_OUT_BEAM);
                npc.GetController().Delete();
                break;
            case 702715:
                TeleportService.TeleportTo(player, instance, 236.1f, 488.86f, 152f, (byte)25, TeleportAnimation.FADE_OUT_BEAM);
                break;
            case 702717:
                TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
                break;
        }
    }

    private void StartTasks()
    {
        tasks = new List<ScheduledTask>();
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5Re_solo_game1_2()), 125000L));
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5Re_solo_game1_3()), 155000L));
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() => SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5Re_solo_game1_4()), 175000L));
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            DeleteAliveNpcs(219958, 219959, 702700, 702701);
            Spawn(702715, 169.366f, 208.93f, 188.02f, (byte)0);
            SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5Re_solo_game1_5());
            SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5Re_solo_game1_6());
        }, 185000L)); // 3min 5sec
    }

    private void CancelTasks()
    {
        if (tasks != null)
        {
            foreach (ScheduledTask future in tasks.Where(future => future != null && !future.IsCancelled))
                future.Cancel(true);
        }
    }

    public override void OnInstanceDestroy()
    {
        CancelTasks();
    }

    public override void LeaveInstance(Player player)
    {
        TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }
}
