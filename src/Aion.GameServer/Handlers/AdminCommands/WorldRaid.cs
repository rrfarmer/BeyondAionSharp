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

    protected override void Execute(Player player, params string[] paramsArr)
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

            int locationId = int.TryParse(paramsArr[1], out var r) ? r : 0;
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

    // Java parity: org.apache.commons.lang3.math.NumberUtils.isNumber(String).
    private static bool IsNumber(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;
        char[] chars = str.ToCharArray();
        int sz = chars.Length;
        bool hasExp = false;
        bool hasDecPoint = false;
        bool allowSigns = false;
        bool foundDigit = false;
        // deal with any possible sign up front
        int start = (chars[0] == '-' || chars[0] == '+') ? 1 : 0;
        if (sz > start + 1 && chars[start] == '0' && (chars[start + 1] == 'x' || chars[start + 1] == 'X'))
        { // leading 0x/0X
            int i = start + 2;
            if (i == sz)
                return false; // str == "0x"
            for (; i < chars.Length; i++)
            {
                if ((chars[i] < '0' || chars[i] > '9') && (chars[i] < 'a' || chars[i] > 'f') && (chars[i] < 'A' || chars[i] > 'F'))
                    return false;
            }
            return true;
        }
        sz--; // don't want to loop to the last char, check it afterwards for type qualifiers
        int idx = start;
        // loop to the next to last char or to the last char if we need another digit to make a valid number (e.g. chars[0..5] = "1234E")
        while (idx < sz || (idx < sz + 1 && allowSigns && !foundDigit))
        {
            if (chars[idx] >= '0' && chars[idx] <= '9')
            {
                foundDigit = true;
                allowSigns = false;
            }
            else if (chars[idx] == '.')
            {
                if (hasDecPoint || hasExp)
                    return false; // two decimal points or dec in exponent
                hasDecPoint = true;
            }
            else if (chars[idx] == 'e' || chars[idx] == 'E')
            {
                if (hasExp)
                    return false; // two E's
                if (!foundDigit)
                    return false;
                hasExp = true;
                allowSigns = true;
            }
            else if (chars[idx] == '+' || chars[idx] == '-')
            {
                if (!allowSigns)
                    return false;
                allowSigns = false;
                foundDigit = false; // we need a digit after the E
            }
            else
            {
                return false;
            }
            idx++;
        }
        if (idx < chars.Length)
        {
            if (chars[idx] >= '0' && chars[idx] <= '9')
                return true; // no type qualifier, OK
            if (chars[idx] == 'e' || chars[idx] == 'E')
                return false; // can't have an E at the last byte
            if (chars[idx] == '.')
            {
                if (hasDecPoint || hasExp)
                    return false;
                return foundDigit; // single trailing decimal point after non-exponent is ok
            }
            if (!allowSigns && (chars[idx] == 'd' || chars[idx] == 'D' || chars[idx] == 'f' || chars[idx] == 'F'))
                return foundDigit;
            if (chars[idx] == 'l' || chars[idx] == 'L')
                return foundDigit && !hasExp && !hasDecPoint; // not allowing L with an exponent or decimal point
            return false; // last character is illegal
        }
        // allowSigns is true iff the val ends in 'E', return !allowSigns to fail when this happens
        return !allowSigns && foundDigit;
    }
}
