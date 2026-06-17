using System.Threading.Tasks;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/LinkgateFoundryInstance (Cheatkiller) : GeneralInstanceHandler. @InstanceID(301270000). instanceRace gating; onDie boss-spawn; handleUseItemFinish device teleports + timed wipe via scheduleAtFixedRate; onEnterZone/onInstanceDestroy task cancel. 1:1.</summary>
[InstanceID(301270000)]
public class LinkgateFoundryInstance : GeneralInstanceHandler
{
    private ScheduledTask timeCheckTask;
    private byte timeInMin = unchecked((byte)-1);
    private byte secretLabEntranceCount = 0;
    private Race? instanceRace;

    public LinkgateFoundryInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnEnterInstance(Player player)
    {
        if (instanceRace == null)
        {
            instanceRace = player.GetRace();
            Spawn(instanceRace == Race.ELYOS ? 206361 : 206362, 348.00464f, 252.13882f, 311.36136f, (byte)10);
        }
    }

    public override void OnDie(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 233898:
            case 234990:
            case 234991:
                Spawn(instanceRace == Race.ELYOS ? 702338 : 702389, 246.74345f, 258.35843f, 312.32327f, (byte)10);
                break;
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 804578:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF4_Re_01_Time_01()); // 20 min
                StartTimeCheck();
                npc.GetController().Die(); // not a static door in client data o.O
                break;
            case 234193:
                npc.GetController().Die();
                break;
            case 804629:
                TeleportService.TeleportTo(player, 301270000, 228.37f, 262.7f, 313, (byte)120);
                break;
            case 702592:
                TeleportService.TeleportTo(player, 301270000, 211.32f, 260, 314, (byte)0, TeleportAnimation.FADE_OUT_BEAM);
                break;
            case 702590:
                TeleportService.TeleportTo(player, 301270000, 257.11f, 323, 271, (byte)60, TeleportAnimation.FADE_OUT_BEAM);
                npc.GetController().Delete();
                secretLabEntranceCount++;
                if (secretLabEntranceCount < 3)
                {
                    Spawn(234992, 244.1839f, 322.5356f, 270.9474f, (byte)0);
                }
                else
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF4_Re_01_secret_room_03());
                }
                break;
        }
    }

    private void StartTimeCheck()
    {
        timeCheckTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            switch (++timeInMin)
            {
                case 5:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF4_Re_01_Time_02());
                    break;
                case 10:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF4_Re_01_Time_03());
                    break;
                case 15:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF4_Re_01_Time_04());
                    break;
                case 17:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF4_Re_01_Time_05());
                    break;
                case 19:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF4_Re_01_Time_06());
                    break;
                case 20:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF4_Re_01_Time_07());
                    instance.ForEachNpc(npc =>
                    {
                        if (npc.GetNpcId() != 233898 && npc.GetNpcId() != 234990 && npc.GetNpcId() != 234991 // belsagos does not despawn
                            && npc.GetNpcId() != 702339 && npc.GetNpcId() != 804629 // teleport device does not despawn
                            && npc.GetNpcId() != 702590 && npc.GetNpcId() != 702591 && npc.GetNpcId() != 234992) // secret lab portal & chest do not despawn
                        {
                            npc.GetController().Delete();
                        }
                    });
                    if (timeCheckTask != null && !timeCheckTask.IsDone())
                    {
                        timeCheckTask.Cancel(true);
                    }
                    break;
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(0L), System.TimeSpan.FromMilliseconds(60000L));
    }

    public override void OnEnterZone(Player player, ZoneInstance zone)
    {
        if (zone.GetAreaTemplate().GetZoneName() == ZoneName.Get("IDLDF4RE_01_ITEMUSEAREA_BOSS_301270000"))
        {
            if (timeCheckTask != null && !timeCheckTask.IsDone())
            {
                timeCheckTask.Cancel(true);
            }
        }
    }

    public override void OnInstanceDestroy()
    {
        if (timeCheckTask != null && !timeCheckTask.IsDone())
        {
            timeCheckTask.Cancel(true);
        }
    }
}
