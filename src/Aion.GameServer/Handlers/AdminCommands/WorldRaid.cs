using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Model.Templates.Worldraid;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/WorldRaid (Whoop, Sykra). Starts/stops the Beritra Invasion event.</summary>
public class WorldRaid : AdminCommand
{
    public WorldRaid()
        : base("worldraid", "Starts/stops the Beritra Invasion event.")
    {
        SetSyntaxInfo(
            "list - Shows all available world raid locations",
            "active - Shows all active world raid locations",
            "start <location_id> - Starts the world raid for the given location",
            "stop <location_id> - Stops the world raid for the given location");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (!EventsConfig.ENABLE_WORLDRAID)
        {
            SendInfo(player, "World raid currently is disabled.");
            return;
        }
        if (paramsArr.Length < 1)
        {
            SendInfo(player);
            return;
        }

        if ("list".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            SendInfo(player, CreateLocationList(DataManager.WORLD_RAID_DATA.GetLocations().Values, "World raid locations:"));
        }
        else if ("active".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            SendInfo(player, CreateLocationList(WorldRaidService.GetInstance().GetActiveWorldRaidLocations(), "Currently active world raids:"));
        }
        else
        {
            if (paramsArr.Length < 2 || !IsNumber(paramsArr[1]))
            {
                SendInfo(player);
                return;
            }

            int locationId = TryParseInt(paramsArr[1], out var r) ? r : 0;
            if (!WorldRaidService.GetInstance().IsValidWorldRaidLocation(locationId))
            {
                SendInfo(player, "Invalid world raid location: " + locationId);
                return;
            }

            if ("start".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
            {
                if (WorldRaidService.GetInstance().IsWorldRaidInProgress(locationId))
                {
                    SendInfo(player, "World raid for location " + locationId + " is already in progress");
                    return;
                }
                SendInfo(player, "Starting world raid for location " + locationId);
                WorldRaidService.GetInstance().StartRaid(locationId, false);
            }
            else if ("stop".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
            {
                if (!WorldRaidService.GetInstance().IsWorldRaidInProgress(locationId))
                {
                    SendInfo(player, "World raid for location " + locationId + " is not started.");
                    return;
                }
                SendInfo(player, "Stopped world raid for location " + locationId);
                WorldRaidService.GetInstance().StopRaid(locationId);
            }
            else
            {
                SendInfo(player);
            }
        }
    }

    private string CreateLocationList(ICollection<WorldRaidLocation> locations, string header)
    {
        StringBuilder sb = new StringBuilder();
        if (header != null && header.Length != 0)
            sb.Append(header);
        if (locations == null || locations.Count == 0)
        {
            sb.Append("\n\tNo locations available!");
            return sb.ToString();
        }

        Dictionary<string, List<WorldRaidLocation>> locationsByMapId = locations.GroupBy(worldRaidLocation =>
        {
            WorldMapTemplate mapTemplate = DataManager.WORLD_MAPS_DATA.GetTemplate(worldRaidLocation.GetMapId());
            if (mapTemplate == null || mapTemplate.GetName().Length == 0)
                return worldRaidLocation.GetMapId().ToString();
            return mapTemplate.GetName();
        }).ToDictionary(g => g.Key, g => g.ToList());

        foreach (string mapName in locationsByMapId.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!locationsByMapId.TryGetValue(mapName, out List<WorldRaidLocation> locationsForMap) || locationsForMap == null)
                continue;
            sb.Append("\n\t").Append(ChatUtil.Color(mapName, System.Drawing.Color.White)).Append(" - ");
            sb.Append(string.Join(", ", locationsForMap.Select(CreatePositionString)));
        }
        return sb.ToString();
    }

    private string CreatePositionString(WorldRaidLocation location)
    {
        return ChatUtil.Position(location.GetLocationId().ToString(), location.GetMapId(), location.GetX(), location.GetY(), location.GetZ());
    }

    // Java parity: NumberUtils.isNumber delegates to Commons Lang 3.20 isCreatable.
    private static bool IsNumber(string str) => IsCreatableNumber(str);
}
