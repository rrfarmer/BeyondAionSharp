using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.LegionDominion;
using Aion.GameServer.Model.Templates.Cp;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.ConquerorAndProtectorSystem;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Taskmanager.Tasks;

/// <summary>Java parity: taskmanager/tasks/LegionDominionIntruderUpdateTask : AbstractPeriodicTaskManager (override Run). stream filter/collect(groupingBy(getWorldId))→Where.GroupBy.ToDictionary; Map.Entry→KeyValuePair (entry==null check vacuous—dropped); isEmpty→Count==0; ZoneName.get→Get, ZoneName.NONE; forEach→foreach. ConquerorAndProtectorService/CPInfo/LegionDominionService red-tolerated.</summary>
public class LegionDominionIntruderUpdateTask : AbstractPeriodicTaskManager
{
    private LegionDominionIntruderUpdateTask() : base(4000)
    {
    }

    protected override void Run()
    {
        if (!CustomConfig.CONQUEROR_AND_PROTECTOR_SYSTEM_ENABLED)
            return;
        Dictionary<int, List<LegionDominionLocation>> locationsPerWorldMap = LegionDominionService.GetInstance().GetLegionDominions()
            .Where(loc => loc.GetLegionId() != 0).GroupBy(loc => loc.GetWorldId()).ToDictionary(g => g.Key, g => g.ToList());
        if (locationsPerWorldMap.Count == 0)
            return;

        foreach (KeyValuePair<int, List<LegionDominionLocation>> entry in locationsPerWorldMap)
        {
            List<Player> players = World.GetInstance().GetWorldMap(entry.Key).GetMainWorldMapInstance().GetPlayersInside();
            foreach (LegionDominionLocation location in entry.Value)
            {
                if (location.GetZoneNameAsString().Length == 0)
                    continue;
                ZoneName zoneName = ZoneName.Get(location.GetZoneNameAsString());
                if (zoneName == ZoneName.NONE)
                    continue;
                ConquerorAndProtectorService cpService = ConquerorAndProtectorService.GetInstance();
                List<Player> protectors = new();
                List<Player> conquerors = new();
                foreach (Player player in players)
                {
                    if (!player.IsInsideZone(zoneName))
                        continue;
                    CPInfo cpInfo = cpService.GetCPInfoForCurrentMap(player);
                    if (cpInfo == null || (cpInfo.GetType_() == CPType.PROTECTOR && cpInfo.GetLDRank() != 3))
                        continue;
                    if (cpInfo.GetType_() == CPType.CONQUEROR)
                        conquerors.Add(player);
                    else if (cpInfo.GetType_() == CPType.PROTECTOR)
                        protectors.Add(player);
                }
                if (conquerors.Count != 0)
                    protectors.ForEach(player => PacketSendUtility.SendPacket(player, new SM_CONQUEROR_PROTECTOR(conquerors, false)));
            }
        }
    }

    public static LegionDominionIntruderUpdateTask GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    private static class SingletonHolder
    {
        internal static readonly LegionDominionIntruderUpdateTask INSTANCE = new();
    }
}
