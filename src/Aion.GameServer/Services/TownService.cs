using System.Collections.Generic;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Spawns.Housing;
using Aion.GameServer.Model.Town;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/TownService (ViAl).</summary>
public class TownService
{
    private static readonly ILogger log = NullLogger.Instance;
    private Dictionary<int, Town> elyosTowns;
    private Dictionary<int, Town> asmosTowns;

    private static class SingletonHolder
    {
        internal static readonly TownService instance = new TownService();
    }

    public static TownService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private TownService()
    {
        elyosTowns = TownDAO.Load(Race.ELYOS);
        asmosTowns = TownDAO.Load(Race.ASMODIANS);
        if (elyosTowns.Count == 0 && asmosTowns.Count == 0)
        {
            foreach (HousingLand land in DataManager.HOUSE_DATA.GetLands())
            {
                foreach (HouseAddress address in land.GetAddresses())
                {
                    if (address.GetTownId() == 0)
                        continue;
                    else
                    {
                        Race townRace = DataManager.NPC_DATA.GetNpcTemplate(land.GetManagerNpcId()).GetTribe() == TribeClass.GENERAL ? Race.ELYOS
                            : Race.ASMODIANS;
                        if ((townRace == Race.ELYOS && !elyosTowns.ContainsKey(address.GetTownId()))
                            || (townRace == Race.ASMODIANS && !asmosTowns.ContainsKey(address.GetTownId())))
                        {
                            Town town = new Town(address.GetTownId(), townRace);
                            if (townRace == Race.ELYOS)
                                elyosTowns[town.GetId()] = town;
                            else
                                asmosTowns[town.GetId()] = town;
                            TownDAO.Store(town);
                        }
                    }
                }
            }
        }
        log.LogInformation("Loaded " + elyosTowns.Count + " elyos towns.");
        log.LogInformation("Loaded " + asmosTowns.Count + " asmodian towns.");
    }

    public Town GetTownById(int townId)
    {
        if (elyosTowns.ContainsKey(townId))
            return elyosTowns[townId];
        else
            return asmosTowns.TryGetValue(townId, out Town town) ? town : null;
    }

    public int GetTownResidence(Player player)
    {
        House house = player.GetActiveHouse();
        if (house == null)
            return 0;
        else
            return house.GetAddress().GetTownId();
    }

    public int GetTownIdByPosition(Creature creature)
    {
        if (creature.GetSpawn() is TownSpawnTemplate townSpawnTemplate)
        {
            return townSpawnTemplate.GetTownId();
        }
        if (creature.IsSpawned())
        {
            foreach (ZoneInstance zone in creature.FindZones())
            {
                if (zone.GetTownId() > 0)
                    return zone.GetTownId();
            }
        }
        return 0;
    }

    public void OnEnterWorld(Player player)
    {
        switch (player.GetRace())
        {
            case Race.ELYOS:
                if (player.GetWorldId() == 700010000)
                    PacketSendUtility.SendPacket(player, new SmTownsList(elyosTowns));
                break;
            case Race.ASMODIANS:
                if (player.GetWorldId() == 710010000)
                    PacketSendUtility.SendPacket(player, new SmTownsList(asmosTowns));
                break;
        }
    }
}
