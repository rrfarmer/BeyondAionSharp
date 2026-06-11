using System;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Utils;

/// <summary>
/// Java parity: utils/PacketSendUtility (Luno, Neon). Static packet-send/broadcast helpers that interact only with their parameters
/// (kept out of Player to keep it a pure data holder). Object... -> params object[]; Predicate<Player>; Runnable -> Action. AIEventType/World red-tolerated.
/// </summary>
public class PacketSendUtility
{
    public static void SendMessage(Player player, string msg)
    {
        SendPacket(player, new SM_MESSAGE(0, null, msg, ChatType.GOLDEN_YELLOW));
    }

    public static void SendMessage(Player player, string msg, ChatType chatType)
    {
        SendPacket(player, new SM_MESSAGE(0, null, msg, chatType));
    }

    /// <summary>Lets the player say a client message to himself (in /s chat and a speech bubble above his head).</summary>
    public static void SendMonologue(Player player, int msgId, params object[] paramsArr)
    {
        SendPacket(player, new SM_SYSTEM_MESSAGE(ChatType.NORMAL, player, msgId, paramsArr));
    }

    /// <summary>Lets the npc say a client message to the player (in /s chat and a speech bubble above the npc's head).</summary>
    public static void SendMessage(Player player, Npc npc, int msgId, params object[] paramsArr)
    {
        SendPacket(player, new SM_SYSTEM_MESSAGE(ChatType.NPC, npc, msgId, paramsArr));
    }

    /// <summary>Lets the npc say a client message to all known players. Use the parameterized overload if you need to pass parameters.</summary>
    public static void BroadcastMessage(Npc npc, int msgId)
    {
        BroadcastMessage(npc, msgId, 0);
    }

    /// <summary>Lets the npc say a client message after the specified delay (ms) to all known players.</summary>
    public static void BroadcastMessage(Npc npc, int msgId, int delay, params object[] msgParams)
    {
        if (npc == null)
            return;
        ScheduleOrRun(() =>
        {
            if (npc.IsSpawned())
                BroadcastPacket(npc, new SM_SYSTEM_MESSAGE(ChatType.NPC, npc, msgId, msgParams), player => PositionUtil.IsInRange(npc, player, 50, false));
        }, delay);
    }

    /// <summary>Sends a packet to the given player.</summary>
    public static void SendPacket(Player player, AionServerPacket packet)
    {
        if (player.IsOnline())
            player.GetClientConnection().SendPacket(packet);
    }

    /// <summary>Broadcasts a packet to all players that the given player knows (and to self if toSelf).</summary>
    public static void BroadcastPacket(Player player, AionServerPacket packet, bool toSelf)
    {
        if (toSelf)
            SendPacket(player, packet);

        BroadcastPacket(player, packet);
    }

    /// <summary>Broadcasts a packet to all players that the given object knows.</summary>
    public static void BroadcastPacket(VisibleObject obj, AionServerPacket packet)
    {
        obj.GetKnownList().ForEachPlayer(player => SendPacket(player, packet));
    }

    /// <summary>Broadcasts a packet to all players that the given object knows and which match the filter.</summary>
    public static void BroadcastPacket(VisibleObject obj, AionServerPacket packet, Predicate<Player> filter)
    {
        obj.GetKnownList().ForEachPlayer(player =>
        {
            if (filter(player))
                SendPacket(player, packet);
        });
    }

    /// <summary>Broadcasts a packet to all players that the given object knows and the object itself, if it's a player.</summary>
    public static void BroadcastPacketAndReceive(VisibleObject visibleObject, AionServerPacket packet)
    {
        if (visibleObject is Player player)
            SendPacket(player, packet);

        BroadcastPacket(visibleObject, packet);
    }

    public static void BroadcastPacketAndReceive(Creature creature, AionServerPacket packet, AIEventType et)
    {
        if (creature is Player player)
            SendPacket(player, packet);

        BroadcastPacketAndAIEvent(creature, packet, et);
    }

    public static void BroadcastPacketAndAIEvent(Creature creature, AionServerPacket packet, AIEventType et)
    {
        creature.GetKnownList().ForEachObject(obj =>
        {
            if (obj is Player player)
                SendPacket(player, packet);
            else if (et != null && obj is Npc npc)
                npc.GetAi().OnCreatureEvent(et, creature);
        });
    }

    /// <summary>Broadcasts a packet to all players that the given object knows and matches the filter (and to self if toSelf).</summary>
    public static void BroadcastPacket(VisibleObject obj, AionServerPacket packet, bool toSelf, Predicate<Player> filter)
    {
        if (toSelf && obj is Player player)
            SendPacket(player, packet);

        obj.GetKnownList().ForEachPlayer(p =>
        {
            if (filter(p))
                SendPacket(p, packet);
        });
    }

    /// <summary>Broadcasts a packet to all logged in players.</summary>
    public static void BroadcastToWorld(AionServerPacket packet)
    {
        World.GetInstance().ForEachPlayer(player => SendPacket(player, packet));
    }

    /// <summary>Broadcasts a packet to all logged in players matching a filter.</summary>
    public static void BroadcastToWorld(AionServerPacket packet, Predicate<Player> filter)
    {
        World.GetInstance().ForEachPlayer(player =>
        {
            if (filter(player))
                SendPacket(player, packet);
        });
    }

    /// <summary>Broadcasts a packet to all online legion members.</summary>
    public static void BroadcastToLegion(Legion legion, AionServerPacket packet)
    {
        foreach (Player onlineLegionMember in legion.GetOnlinePlayers())
        {
            SendPacket(onlineLegionMember, packet);
        }
    }

    public static void BroadcastToLegion(Legion legion, AionServerPacket packet, int playerObjId)
    {
        foreach (Player onlineLegionMember in legion.GetOnlinePlayers())
        {
            if (onlineLegionMember.GetObjectId() != playerObjId)
                SendPacket(onlineLegionMember, packet);
        }
    }

    /// <summary>Broadcasts a packet to all players who can see the specified object.</summary>
    public static void BroadcastToSightedPlayers(VisibleObject obj, AionServerPacket packet)
    {
        BroadcastToSightedPlayers(obj, packet, false);
    }

    /// <summary>Broadcasts a packet to all players who can see the specified object.</summary>
    public static void BroadcastToSightedPlayers(VisibleObject obj, AionServerPacket packet, bool toSelf)
    {
        BroadcastPacket(obj, packet, toSelf, other => other.GetKnownList().Sees(obj));
    }

    /// <summary>Broadcasts the system message to all players that are on the same map instance as the object.</summary>
    public static void BroadcastToMap(VisibleObject obj, int msgId)
    {
        BroadcastToMap(obj.GetPosition().GetWorldMapInstance(), new SM_SYSTEM_MESSAGE(msgId), 0, p => !obj.Equals(p));
    }

    /// <summary>Broadcasts the system message after the specified delay (ms) to all players on the same map instance as the object.</summary>
    public static void BroadcastToMap(VisibleObject obj, int msgId, int delay)
    {
        BroadcastToMap(obj.GetPosition().GetWorldMapInstance(), new SM_SYSTEM_MESSAGE(msgId), delay, p => !obj.Equals(p));
    }

    /// <summary>Broadcasts a packet to all players that are on the same map instance as the object.</summary>
    public static void BroadcastToMap(VisibleObject obj, AionServerPacket packet)
    {
        BroadcastToMap(obj.GetPosition().GetWorldMapInstance(), packet, 0, p => !obj.Equals(p));
    }

    /// <summary>Broadcasts a packet after the specified delay (ms) to all players on the same map instance as the object.</summary>
    public static void BroadcastToMap(VisibleObject obj, AionServerPacket packet, int delay)
    {
        BroadcastToMap(obj.GetPosition().GetWorldMapInstance(), packet, delay, p => !obj.Equals(p));
    }

    /// <summary>Broadcasts a packet after the specified delay (ms) to all players on the same map instance as the object and matching the filter.</summary>
    public static void BroadcastToMap(VisibleObject obj, AionServerPacket packet, int delay, Predicate<Player> filter)
    {
        BroadcastToMap(obj.GetPosition().GetWorldMapInstance(), packet, delay, filter);
    }

    /// <summary>Broadcasts a packet to all players on the specified map instance.</summary>
    public static void BroadcastToMap(WorldMapInstance mapInstance, AionServerPacket packet)
    {
        BroadcastToMap(mapInstance, packet, 0, player => true);
    }

    /// <summary>Broadcasts a packet after the specified delay (ms) to all players on the specified map instance.</summary>
    public static void BroadcastToMap(WorldMapInstance mapInstance, AionServerPacket packet, int delay)
    {
        BroadcastToMap(mapInstance, packet, delay, player => true);
    }

    /// <summary>Broadcasts a packet after the specified delay (ms) to all players on the specified map instance and matching the filter.</summary>
    public static void BroadcastToMap(WorldMapInstance mapInstance, AionServerPacket packet, int delay, Predicate<Player> filter)
    {
        ScheduleOrRun(() => mapInstance.ForEachPlayer(player =>
        {
            if (filter(player))
                SendPacket(player, packet);
        }), delay);
    }

    public static void BroadcastToZone(ZoneInstance zone, AionServerPacket packet)
    {
        ScheduleOrRun(() => zone.ForEach(creature =>
        {
            if (creature is Player player)
                SendPacket(player, packet);
        }), 0);
    }

    private static void ScheduleOrRun(Action r, int delay)
    {
        if (delay <= 0)
            r();
        else
            ThreadPoolManager.GetInstance().Schedule(r, delay);
    }
}
