using System.Threading;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Flyring;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/DraupnirCaveInstance (Sykra) : GeneralInstanceHandler. @InstanceID(320080000); AtomicBoolean compareAndSet→int+Interlocked.CompareExchange==0; Rnd.get(1,4) arrow-switch→C# switch; FlyRing/onPassFlyingRing/onDie/onReviveEvent 1:1.</summary>
[InstanceID(320080000)]
public class DraupnirCaveInstance : GeneralInstanceHandler
{
    private int isInstanceStartMessageSend;
    private int adjutantsKilled;

    public DraupnirCaveInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        new FlyRing(new FlyRingTemplate("INSTANCE_MSG_START", mapId, new Point3D(475.8222f, 427.8852f, 618.3719f),
            new Point3D(478.3698f, 450.1829f, 624.6974f), new Point3D(473.2742f, 405.5875f, 612.0464f), 20), instance.GetInstanceId()).Spawn();

        switch (Rnd.Get(1, 4))
        {
            case 1:
                Spawn(213587, 567.438f, 700.875f, 538.701f, (byte)7); // Hungry Ooze
                break;
            case 2:
                Spawn(213588, 166.8f, 536.285f, 505.802f, (byte)9); // Lucky Golden Saam
                break;
            case 3:
                Spawn(213771, 497.006f, 434.713f, 616.584f, (byte)71); // Protector Rakkan
                break;
            case 4:
                Spawn(213773, 380.694f, 611.956f, 598.523f, (byte)98); // Dragonpriest Tairgus
                break;
        }
    }

    public override bool OnPassFlyingRing(Player player, string flyingRing)
    {
        if ("INSTANCE_MSG_START".Equals(flyingRing) && Interlocked.CompareExchange(ref isInstanceStartMessageSend, 1, 0) == 0)
        {
            SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_BOSS_SPAWN_IDDF3_DRAGON_1());
            return true;
        }
        return false;
    }

    public override void OnDie(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 213776: // Instructor Afrane
            case 213778: // Beautiful Lakshmi
            case 213779: // Commander Nimbarka
            case 213802: // Kind Saraswati
                switch (++adjutantsKilled)
                {
                    case 1:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_BOSS_SPAWN_IDDF3_DRAGON_2());
                        break;
                    case 2:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_BOSS_SPAWN_IDDF3_DRAGON_3());
                        break;
                    case 3:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_BOSS_SPAWN_IDDF3_DRAGON_4());
                        break;
                    case 4:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_BOSS_SPAWN_IDDF3_DRAGON());
                        Spawn(213780, 813.189f, 432.255f, 318.928f, (byte)30); // Commander Bakarma
                        break;
                }
                break;
        }
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, true, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        TeleportService.TeleportTo(player, instance, 492.83383f, 375.46542f, 622.26920f, (byte)29);
        return true;
    }
}
