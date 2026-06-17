using System.Threading;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/OphidanBridgeInstance (Yeats) : GeneralInstanceHandler. @InstanceID(300590000). instance starts always in hardmode (isNormalMode); AtomicBoolean→int+Interlocked; Future→ScheduledTask. onDie boss/endboss kills + enrage; handleUseItemFinish bridge control + exit; onReviveEvent. 1:1.</summary>
[InstanceID(300590000)]
public class OphidanBridgeInstance : GeneralInstanceHandler
{
    private int isNormalMode; // instance starts always in hardmode
    private int used;
    private byte bossKills = 0;
    private byte endBossKills = 0;
    private ScheduledTask task;

    public OphidanBridgeInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceDestroy()
    {
        if (task != null && !task.IsCancelled)
        {
            task.Cancel(true);
        }
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 235764: // Escapee Asachin, triggers normal mode
                Interlocked.Exchange(ref isNormalMode, 1);
                break;
            case 235775:
            case 235774:
            case 235773:
            case 235772:
                if (Volatile.Read(ref isNormalMode) == 0)
                {
                    DeleteAliveNpcs(235764);
                }
                bossKills++;
                if (bossKills == 3)
                {
                    Spawn(701644, 436.99045f, 496.55634f, 605.95203f, (byte)2); // Bridge Control
                    if (Volatile.Read(ref isNormalMode) == 0)
                    {
                        SpawnRandomBosses();
                    }
                    else
                    {
                        Spawn(235768, 323.016f, 489.295f, 607.645f, (byte)0); // Spirited Velkur
                    }
                }
                break;
            case 235759:
            case 235763:
            case 235767:
            case 235768:
            case 235769:
            case 235770:
            case 235771:
                endBossKills++;
                if (endBossKills == (Volatile.Read(ref isNormalMode) != 0 ? 1 : 2))
                {
                    Spawn(730868, 315.85f, 488.65f, 607.6435f, (byte)0); // Ophidan Bridge exit
                }
                else
                {
                    ScheduleEnrage();
                }
                break;
            case 235786: // Steel Wall
                npc.GetController().Delete();
                break;
        }
    }

    private void ScheduleEnrage()
    {
        task = ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (endBossKills < 2)
                Spawn(856059, 322.4960f, 488.3117f, 656.4463f, (byte)50);
        }, 10000L);
    }

    private void SpawnRandomBosses()
    {
        int npcId = 235759 + (Rnd.Get(0, 2) * 4);
        Spawn(npcId, 325.1554f, 483.4476f, 607.6434f, (byte)0);
        switch (npcId)
        {
            case 235759: Spawn(235769, 323.016f, 489.295f, 607.645f, (byte)0); break; // Velkur Aethercaster
            case 235763: Spawn(235770, 323.016f, 489.295f, 607.645f, (byte)0); break; // Velkur Aetherpriest
            case 235767: Spawn(235771, 323.016f, 489.295f, 607.645f, (byte)0); break; // Velkur Aetherknife
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 701644:
                if (Interlocked.CompareExchange(ref used, 1, 0) == 0)
                {
                    npc.GetController().Delete();
                    Spawn(731544, 436.36f, 496.45f, 604.8871f, (byte)2, 7);
                }
                break;
            case 731544:
                TeleportService.TeleportTo(player, player.GetWorldId(), 369.532f, 491.7861f, 605.696f, (byte)60, TeleportAnimation.FADE_OUT_BEAM);
                break;
            case 730868:
                InstanceService.MoveToExitPoint(player);
                break;
        }
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, true, 0);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        TeleportService.TeleportTo(player, instance, 755.21f, 559.292f, 572.9508f, (byte)86);
        return true;
    }
}
