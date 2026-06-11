using System.Collections.Generic;
using Aion.GameServer.Controllers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Rift;

/// <summary>Java parity: services/rift/RiftInformer (Source). sendRiftsInfo (world/player, twin map), sendRiftInfo(int[]), sendRiftDespawn; getPackets builds SM_RIFT_ANNOUNCE lists; syncRiftsState to player / world-map players (anonymous Consumer->lambda); getAnnounceData (TreeMap->SortedDictionary, 12 announce slots); calcRiftsData (Java switch on RiftEnum constants -> reference-equality group sets since RiftEnum is a class); getTwinId map. RVController/SM_RIFT_ANNOUNCE red-tolerated.</summary>
public class RiftInformer
{
    private static readonly HashSet<RiftEnum> VortexGroup = new HashSet<RiftEnum>
    {
        RiftEnum.MARCHUTAN_AM, RiftEnum.KAISINEL_AM
    };

    private static readonly HashSet<RiftEnum> NormalGroup = new HashSet<RiftEnum>
    {
        RiftEnum.MORHEIM_AM, RiftEnum.MORHEIM_BM, RiftEnum.MORHEIM_CM, RiftEnum.MORHEIM_DM, RiftEnum.MORHEIM_EM, RiftEnum.MORHEIM_FM, RiftEnum.MORHEIM_GM,
        RiftEnum.BELUSLAN_AM, RiftEnum.BELUSLAN_BM, RiftEnum.BELUSLAN_CM, RiftEnum.BELUSLAN_DM, RiftEnum.BELUSLAN_EM, RiftEnum.BELUSLAN_FM, RiftEnum.BELUSLAN_GM,
        RiftEnum.GELKMAROS_AM, RiftEnum.GELKMAROS_BM, RiftEnum.GELKMAROS_CM, RiftEnum.GELKMAROS_DM,
        RiftEnum.ENSHAR_AM, RiftEnum.ENSHAR_BM, RiftEnum.ENSHAR_CM, RiftEnum.ENSHAR_DM, RiftEnum.ENSHAR_EM, RiftEnum.ENSHAR_FM,
        RiftEnum.ELTNEN_AM, RiftEnum.ELTNEN_BM, RiftEnum.ELTNEN_CM, RiftEnum.ELTNEN_DM, RiftEnum.ELTNEN_EM, RiftEnum.ELTNEN_FM, RiftEnum.ELTNEN_GM,
        RiftEnum.HEIRON_AM, RiftEnum.HEIRON_BM, RiftEnum.HEIRON_CM, RiftEnum.HEIRON_DM, RiftEnum.HEIRON_EM, RiftEnum.HEIRON_FM, RiftEnum.HEIRON_GM,
        RiftEnum.INGGISON_AM, RiftEnum.INGGISON_BM, RiftEnum.INGGISON_CM, RiftEnum.INGGISON_DM,
        RiftEnum.CYGNEA_AM, RiftEnum.CYGNEA_BM, RiftEnum.CYGNEA_CM, RiftEnum.CYGNEA_DM, RiftEnum.CYGNEA_EM, RiftEnum.CYGNEA_FM
    };

    private static readonly HashSet<RiftEnum> VolatileGroup = new HashSet<RiftEnum>
    {
        RiftEnum.ENSHAR_GM, RiftEnum.ENSHAR_HM, RiftEnum.ENSHAR_IM,
        RiftEnum.CYGNEA_GM, RiftEnum.CYGNEA_HM, RiftEnum.CYGNEA_IM
    };

    public static void SendRiftsInfo(int worldId)
    {
        SyncRiftsState(worldId, GetPackets(worldId));
        int twinId = GetTwinId(worldId);
        if (twinId > 0)
            SyncRiftsState(twinId, GetPackets(twinId));
    }

    public static void SendRiftsInfo(Player player)
    {
        SyncRiftsState(player, GetPackets(player.GetWorldId()));
        int twinId = GetTwinId(player.GetWorldId());
        if (twinId > 0)
            SyncRiftsState(twinId, GetPackets(twinId));
    }

    public static void SendRiftInfo(int[] worlds)
    {
        foreach (int worldId in worlds)
            SyncRiftsState(worldId, GetPackets(worlds[0], -1));
    }

    public static void SendRiftDespawn(int worldId, int objId)
    {
        SyncRiftsState(worldId, GetPackets(worldId, objId), true);
    }

    private static List<AionServerPacket> GetPackets(int worldId)
    {
        return GetPackets(worldId, 0);
    }

    private static List<AionServerPacket> GetPackets(int worldId, int objId)
    {
        List<AionServerPacket> packets = new List<AionServerPacket>();
        if (objId == -1)
        {
            foreach (Npc rift in RiftManager.GetSpawnedRifts(worldId))
            {
                RVController controller = (RVController)rift.GetController();
                if (!controller.IsMaster())
                {
                    continue;
                }

                packets.Add(new SM_RIFT_ANNOUNCE(controller, false));
            }
        }
        else if (objId > 0)
        {
            packets.Add(new SM_RIFT_ANNOUNCE(objId));
        }
        else
        {
            packets.Add(new SM_RIFT_ANNOUNCE(GetAnnounceData(worldId)));
            foreach (Npc rift in RiftManager.GetSpawnedRifts(worldId))
            {
                RVController controller = (RVController)rift.GetController();
                if (!controller.IsMaster())
                {
                    continue;
                }
                packets.Add(new SM_RIFT_ANNOUNCE(controller, true));
                packets.Add(new SM_RIFT_ANNOUNCE(controller, false));
            }
        }
        return packets;
    }

    /*
     * Sends generated rift info packets to player
     */
    private static void SyncRiftsState(Player player, List<AionServerPacket> packets)
    {
        foreach (AionServerPacket packet in packets)
        {
            PacketSendUtility.SendPacket(player, packet);
        }
    }

    /*
     * Sends generated rift info packets to all players within world
     */
    private static void SyncRiftsState(int worldId, List<AionServerPacket> packets)
    {
        SyncRiftsState(worldId, packets, false);
    }

    private static void SyncRiftsState(int worldId, List<AionServerPacket> packets, bool isDespawnInfo)
    {
        World.GetInstance().GetWorldMap(worldId).GetMainWorldMapInstance().ForEachPlayer(player =>
        {
            SyncRiftsState(player, packets);
        });
    }

    private static SortedDictionary<int, int> GetAnnounceData(int worldId)
    {
        SortedDictionary<int, int> localRifts = new SortedDictionary<int, int>();

        /**
         * init empty list
         * 12 different announces
         * we have to send them all otherwise the client announces them even if they are not spawned
         * looks like it depends on which map you are. But its always: first 6 indexes are from [current map] to [other map].
         * whereas the last 6 indexes are from [other map] to [current map].
         * index - announce
         * 0 - normal rift
         * 1 - vortex rift opened
         * 2 - rift to concert hall
         * 3 - rift to pangaea/ahserion
         * 4 - volatile rift
         * 5 - infiltration rift
         *
         * 6 - normal rift
         * 7 - vortex rift opened
         * 8 - rift to concert hall
         * 9 - rift to pangaea/ahserion
         * 10 - volatile rift
         * 11 - infiltration rift
         */
        for (int i = 0; i < 12; i++)
        {
            localRifts[i] = 0;
        }


        foreach (Npc rift in RiftManager.GetSpawnedRifts(worldId))
        {
            RVController rc = (RVController)rift.GetController();
            localRifts = CalcRiftsData(rc, localRifts);
        }

        return localRifts;
    }

    private static SortedDictionary<int, int> CalcRiftsData(RVController rift, SortedDictionary<int, int> local)
    {
        if (rift.IsMaster())
        {
            RiftEnum template = rift.GetRiftTemplate();
            if (VortexGroup.Contains(template))
            {
                local[1] = local[1] + 1; // vortex rift
            }
            else if (NormalGroup.Contains(template))
            {
                local[0] = local[0] + 1; // normal rift
            }
            else if (VolatileGroup.Contains(template))
            {
                if (rift.IsVolatile())
                {
                    local[4] = local[4] + 1; // chaos rift
                }
                else
                {
                    local[0] = local[0] + 1; // normal rift
                }
            }
        }
        return local;
    }

    private static int GetTwinId(int worldId)
    {
        switch (worldId)
        {
            case 110070000: // Kaisinel Academy -> Brusthonin
                return 220050000;
            case 210020000: // Eltnen -> Morheim
                return 220020000;
            case 210040000: // Heiron -> Beluslan
                return 220040000;
            case 210050000: // Inggison -> Gelkmaros
                return 220070000;
            case 210060000: // Theobomos -> Marchutan Priory
                return 120080000;
            case 210070000: // Cygnea -> Enshar
                return 220080000;
            case 120080000: // Marchutan Priory -> Theobomos
                return 210060000;
            case 220020000: // Morheim -> Eltnen
                return 210020000;
            case 220040000: // Beluslan -> Heiron
                return 210040000;
            case 220050000: // Brusthonin -> Kaisinel Academy
                return 110070000;
            case 220070000: // Gelkmaros -> Inggison
                return 210050000;
            case 220080000: // Enshar -> Cygnea
                return 210070000;
            default:
                return 0;
        }
    }
}
