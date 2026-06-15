using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/HouseCommand (Rolandas).</summary>
public class HouseCommand : AdminCommand
{
    public HouseCommand()
        : base("house", "House teleport and ownership management.")
    {
        SetSyntaxInfo(
            "list - Shows all maps with houses.",
            "list <map> - Shows all house addresses for the given map.",
            "tp <address> - Teleports you to the house with the given address.",
            "own <address> - Gives ownership of given house to your target.",
            "revoke <address> - Revokes ownership of given house.",
            "reloadscripts <address> - Reloads all scripts for the given house."
        );
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        House house = null;
        if (paramsArr.Length >= 2)
        {
            int address = ToInt(paramsArr[1]);
            house = HousingService.GetInstance().GetHouseByAddress(address);
        }
        if (house == null && !"list".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            SendInfo(admin, "Invalid address.");
            return;
        }
        if ("list".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr.Length == 1)
                ListMapsWithHouses(admin);
            else
                ListHouses(admin, WorldMapTypeExtensions.Of(paramsArr[1]));
        }
        else if ("own".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            AcquireHouse(admin, house);
        }
        else if ("revoke".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            RevokeOwnership(admin, house);
        }
        else if ("tp".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            TeleportService.TeleportTo(admin, house.GetPosition().GetWorldMapInstance(), house.GetX(), house.GetY(), house.GetZ(),
                (byte) house.GetTeleportHeading(), TeleportAnimation.NONE);
        }
        else if ("reloadscripts".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            ReloadPlayerScripts(admin, house);
        }
        else
        {
            SendInfo(admin);
        }
    }

    private void ListMapsWithHouses(Player admin)
    {
        string maps = string.Join("\n\t", HousingService.GetInstance().GetCustomHouses()
            .Select(house => WorldMapTypeExtensions.GetWorld(house.GetAddress().GetMapId()))
            .Distinct().OrderBy(worldMapType => worldMapType)
            .Select(worldMapType => ChatUtil.Color(CapitalizeFully(worldMapType.ToString()), Color.White)));
        SendInfo(admin, "Maps with houses:\n\t" + maps + "\nType " + ChatUtil.Color(GetAliasWithPrefix() + " list mapname", Color.White)
            + " to show all houses for that map.");
    }

    private void ListHouses(Player admin, WorldMapType? worldMapType)
    {
        if (worldMapType == null)
        {
            SendInfo(admin, "Invalid map name.");
            return;
        }
        Dictionary<HouseType, List<House>> housesByType = GetHousesByType(worldMapType.Value.GetId());
        if (housesByType.Count == 0)
        {
            SendInfo(admin, "There are no houses in " + CapitalizeFully(worldMapType.ToString()));
            return;
        }
        SendInfo(admin, "Houses in " + CapitalizeFully(worldMapType.ToString()) + ":");
        foreach (KeyValuePair<HouseType, List<House>> entry in housesByType)
            SendInfo(admin, CapitalizeFully(entry.Key.ToString()) + ":\n\t" + FormatAddresses(entry.Value));
    }

    private string FormatAddresses(List<House> houses)
    {
        bool dash = false;
        int lastAddress = houses[0].GetAddress().GetId();
        string addresses = ChatUtil.Color(lastAddress + "", Color.White);
        for (int i = 1; i < houses.Count; i++)
        {
            House house = houses[i];
            int currentAddress = house.GetAddress().GetId();
            if (lastAddress + 1 != currentAddress || i + 1 == houses.Count || currentAddress + 1 != houses[i + 1].GetAddress().GetId())
            {
                if (!string.IsNullOrEmpty(addresses) && !dash)
                    addresses += ", ";
                addresses += ChatUtil.Color(currentAddress + "", Color.White);
                dash = false;
            }
            else if (!dash)
            {
                addresses += "-";
                dash = true;
            }
            lastAddress = house.GetAddress().GetId();
        }
        return addresses;
    }

    private Dictionary<HouseType, List<House>> GetHousesByType(int mapId)
    {
        // Java parity: comparator = comparing(houseType.id).reversed().thenComparing(address.id)
        List<House> sorted = HousingService.GetInstance().GetCustomHouses()
            .Where(house => house.GetAddress().GetMapId() == mapId)
            .OrderByDescending(house => house.GetHouseType().GetId())
            .ThenBy(house => house.GetAddress().GetId())
            .ToList();
        Dictionary<HouseType, List<House>> result = new Dictionary<HouseType, List<House>>();
        foreach (House house in sorted)
        {
            if (!result.TryGetValue(house.GetHouseType(), out List<House> list))
            {
                list = new List<House>();
                result[house.GetHouseType()] = list;
            }
            list.Add(house);
        }
        return result;
    }

    private void AcquireHouse(Player admin, House house)
    {
        VisibleObject creature = admin.GetTarget();
        if (!(creature is Player target))
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        if (house.GetOwnerId() == target.GetObjectId())
        {
            SendInfo(admin, target.GetName() + " already owns that house.");
            return;
        }
        if (target.GetHouses().Count >= 2)
        {
            SendInfo(admin, target.GetName() + " must sell his old house which is currently in grace time first!");
            return;
        }
        House studio = HousingService.GetInstance().GetPlayerStudio(target.GetObjectId());
        if (studio != null)
            HousingService.GetInstance().ChangeOwner(studio, 0);
        HousingService.GetInstance().ChangeOwner(house, target.GetObjectId());
        SendInfo(admin, "House " + house.GetName() + " is now owned by " + target.GetName());
    }

    private void RevokeOwnership(Player admin, House house)
    {
        int ownerId = house.GetOwnerId();
        if (ownerId == 0)
        {
            SendInfo(admin, "House has no owner.");
            return;
        }
        HousingService.GetInstance().ChangeOwner(house, 0);
        SendInfo(admin, "Ownership of house " + house.GetAddress().GetId() + " was revoked from " + PlayerService.GetPlayerName(ownerId));
    }

    private void ReloadPlayerScripts(Player admin, House house)
    {
        Npc butler = house.GetButler();
        if (butler == null)
        {
            SendInfo(admin, "No butler was found for house with address " + house.GetAddress().GetId());
            return;
        }
        house.ReloadPlayerScripts();
        butler.GetKnownList().ForEachPlayer(house.SendScripts);
        SendInfo(admin, "Script reload successful");
    }

    // Java parity: org.apache.commons.lang3.math.NumberUtils.toInt(String) — returns 0 if not parseable.
    private static int ToInt(string str)
    {
        return int.TryParse(str, out int result) ? result : 0;
    }

    // Java parity: org.apache.commons.lang3.text.WordUtils.capitalizeFully(String).
    private static string CapitalizeFully(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        str = str.ToLower();
        char[] buffer = str.ToCharArray();
        bool capitalizeNext = true;
        for (int i = 0; i < buffer.Length; i++)
        {
            char ch = buffer[i];
            if (char.IsWhiteSpace(ch))
            {
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                buffer[i] = char.ToUpper(ch);
                capitalizeNext = false;
            }
        }
        return new string(buffer);
    }
}
