using System.Linq;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/EsoterraceInstance (xTz, Gigi) : GeneralInstanceHandler. @InstanceID(300250000); setDoorState/onDie templateId switch (door states, hardmode Surkana Feeder, keening sirokin chest broadcastMessage 342359, Kexkra wall collapse), onReviveEvent revive 25/25, onEnterZone DRANA_PRODUCTION_LAB SM 1400919; instance.getNpcs(...).stream().allMatch(Creature::isDead) → LINQ All 1:1.</summary>
[InstanceID(300250000)]
public class EsoterraceInstance : GeneralInstanceHandler
{
    public EsoterraceInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        instance.SetDoorState(367, true);
        if (Rnd.Chance() < 21)
        {
            Spawn(799580, 1034.11f, 985.01f, 327.35095f, (byte)105);
            Spawn(217649, 1033.67f, 978.08f, 327.35095f, (byte)35);
        }
    }

    public override void OnDie(Npc npc)
    {
        switch (npc.GetObjectTemplate().GetTemplateId())
        {
            case 282295:
                instance.SetDoorState(39, true);
                break;
            case 282291: // Surkana Feeder enables "hardmode"
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDF4Re_Drana_08());
                DeleteAliveNpcs(217204);
                Spawn(217205, 1315.43f, 1171.04f, 51.8054f, (byte)66);
                break;
            case 217289:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDF4Re_Drana_07());
                instance.SetDoorState(122, true);
                break;
            case 217281:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDF4Re_Drana_04());
                instance.SetDoorState(70, true);
                break;
            case 217195:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDF4Re_Drana_05());
                instance.SetDoorState(45, true);
                instance.SetDoorState(52, true);
                instance.SetDoorState(67, true);
                Spawn(701027, 751.513489f, 1136.021851f, 365.031158f, (byte)60, 41);
                Spawn(701027, 829.620789f, 1134.330078f, 365.031281f, (byte)60, 77);
                break;
            case 217185:
                Spawn(701023, 1264.862061f, 644.995178f, 296.831818f, (byte)60, 112);
                instance.SetDoorState(367, false);
                break;
            case 217204:
                instance.SetDoorState(78, true); // collapse walls of Kexkra's chamber
                Spawn(205437, 1309.390259f, 1163.644287f, 51.493992f, (byte)13);
                Spawn(701027, 1318.669800f, 1180.467651f, 52.879887f, (byte)75, 727);
                break;
            case 217206:
                Spawn(205437, 1309.390259f, 1163.644287f, 51.493992f, (byte)13);
                Spawn(701027, 1318.669800f, 1180.467651f, 52.879887f, (byte)75, 727);
                Spawn(701027, 1325.484497f, 1173.198486f, 52.879887f, (byte)75, 726);
                break;
            case 217649:
                // keening sirokin treasure chest
                Npc keeningSirokin = GetNpc(799580);
                Spawn(701025, 1038.63f, 987.74f, 328.356f, (byte)0, 725);
                PacketSendUtility.BroadcastMessage(keeningSirokin, 342359);
                keeningSirokin.GetController().Delete();
                break;
            case 217282:
            case 217283:
            case 217284:
                if (instance.GetNpcs(217282, 217283, 217284).All(c => c.IsDead()))
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDF4Re_Drana_03());
                    instance.SetDoorState(111, true);
                }
                break;
        }
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        TeleportService.TeleportTo(player, instance, 384.57535f, 535.4073f, 321.6642f, (byte)17);
        return true;
    }

    public override void OnEnterZone(Player player, ZoneInstance zone)
    {
        if (zone.GetAreaTemplate().GetZoneName() == ZoneName.Get("DRANA_PRODUCTION_LAB_300250000"))
        {
            PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(1400919));
        }
    }
}
