using System.Drawing;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Custom.Pvpmap;

/// <summary>Java parity: custom/pvpmap/PvpMapService (Yeats). Singleton. java.awt.Color.WHITE→System.Drawing.Color.White (ChatUtil.Color(string,Color?) overload restored); PvpMapHandler::new→() => new PvpMapHandler(). PvpMapHandler/InstanceService/WorldMapInstance red-tolerated.</summary>
public class PvpMapService
{
    private static readonly PvpMapService instance = new PvpMapService();
    private PvpMapHandler handler;

    public static PvpMapService GetInstance()
    {
        return instance;
    }

    public void Init()
    {
        WorldMapInstance instance = InstanceService.GetNextAvailableInstance(301220000, 0, (byte)0, () => new PvpMapHandler(), 0, false);
        handler = (PvpMapHandler)instance.GetInstanceHandler();
    }

    public void OnLogin(Player player)
    {
        if (handler != null && CustomConfig.PVP_MAP_ENABLED && handler.IsRandomBossAlive())
            NotifyBossSpawn(player);
    }

    public void NotifyBossSpawn(Player player)
    {
        bool isOnPvpMap = IsOnPvPMap(player);
        if (!isOnPvpMap && player.IsInInstance()) // don't notify players inside instances
            return;
        bool isLvSixtyOrHigher = player.GetLevel() >= 60;
        if (isOnPvpMap || isLvSixtyOrHigher)
            PacketSendUtility.SendMessage(player, "[PvP-Map] A powerful monster appeared.", ChatType.BRIGHT_YELLOW_CENTER);
        if (!isOnPvpMap && isLvSixtyOrHigher)
            PacketSendUtility.SendMessage(player, "You can join the map via " + ChatUtil.Color(".pvp join", Color.White), ChatType.BRIGHT_YELLOW);
    }

    public bool IsRandomBoss(Npc npc)
    {
        return handler != null && handler.IsRandomBoss(npc.GetObjectId());
    }

    public void JoinMap(Player p)
    {
        if (handler != null && !handler.IsOnMap(p))
            handler.Join(p);
    }

    public void LeaveMap(Player p)
    {
        if (handler != null && handler.IsOnMap(p))
            handler.Leave(p);
    }

    public bool IsOnPvPMap(Creature creature)
    {
        return handler != null && handler.IsOnMap(creature);
    }

    public int GetParticipantsSize()
    {
        return handler == null ? 0 : handler.GetParticipantsSize();
    }

    internal void OnInstanceDestroy()
    {
        handler = null;
    }
}
