using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Siege;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/SiegeCommand.</summary>
public class SiegeCommand : AdminCommand
{
    public SiegeCommand()
        : base("siege", "Controls sieges and artifacts.")
    {
        SetSyntaxInfo(
            "locations - Shows info about all locations.",
            "start <locationId> - Starts the siege at the given location.",
            "stop <locationId> - Stops the siege at the given location.",
            "capture <locationId> [elyos|asmodians|balaur|legionName|legionId] - Captures the fortress at given location.",
            "assault <locationId> [delaySec] - Starts an assault at the given location."
        );
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        switch (paramsArr.Length == 0 ? "" : paramsArr[0].ToLower())
        {
            case "locations":
                ListLocations(player);
                break;
            case "start":
                StartSiege(player, ParseLocation(paramsArr));
                break;
            case "stop":
                StopSiege(player, ParseLocation(paramsArr));
                break;
            case "capture":
                Capture(player, ParseLocation(paramsArr), paramsArr);
                break;
            case "assault":
                Assault(player, ParseLocation(paramsArr), paramsArr.Length < 3 ? 0 : int.Parse(paramsArr[2]));
                break;
            default:
                SendInfo(player);
                break;
        }
    }

    private void ListLocations(Player player)
    {
        IEnumerable<List<SiegeLocation>> locations = SiegeService.GetInstance().GetSiegeLocations().Values
            .OrderBy(l => l.GetLocationId())
            .GroupBy(l => (l.GetLocationId() - 1) / 10)
            .Select(g => g.ToList());
        foreach (List<SiegeLocation> siegeLocations in locations)
        {
            for (int i = 0; i < siegeLocations.Count; i++)
            {
                SiegeLocation loc = siegeLocations[i];
                string worldName = DataManager.WORLD_MAPS_DATA.GetTemplate(loc.GetTemplate().GetWorldId()).GetName();
                string name = loc.GetTemplate().GetL10nId() == 0 ? loc.GetType_().ToString() : loc.GetTemplate().GetL10n();
                string message = name + " (ID: " + loc.GetLocationId() + ") in " + worldName + " belongs to " + loc.GetRace();
                int secondsLeft = SiegeService.GetInstance().GetRemainingSiegeTimeInSeconds(loc.GetLocationId());
                if (secondsLeft > 0)
                    message += " (" + secondsLeft / 60 + "m " + secondsLeft % 60 + "s until siege ends)";
                if (i > 0 && loc.GetType_() == SiegeType.ARTIFACT)
                    message = '\t' + message;
                SendInfo(player, message);
            }
        }
    }

    private void StartSiege(Player player, SiegeLocation loc)
    {
        if (SiegeService.GetInstance().IsSiegeInProgress(loc.GetLocationId()))
        {
            SendInfo(player, "This location is already under siege.");
        }
        else
        {
            SiegeService.GetInstance().StartSiege(loc.GetLocationId());
            SendInfo(player, "Started siege at " + GetLocationName(loc));
        }
    }

    private void StopSiege(Player player, SiegeLocation loc)
    {
        if (!SiegeService.GetInstance().IsSiegeInProgress(loc.GetLocationId()))
        {
            SendInfo(player, "This location is not under siege.");
        }
        else
        {
            SiegeService.GetInstance().StopSiege(loc.GetLocationId());
            SendInfo(player, "Stopped siege at " + GetLocationName(loc));
        }
    }

    private void Capture(Player player, SiegeLocation loc, string[] paramsArr)
    {
        SiegeRace? sr = null;
        Legion legion = null;
        if (paramsArr.Length >= 3)
        {
            // Java parity: SiegeRace.valueOf(name) — matches by enum constant name only (not ordinal/numeric).
            string raceName = paramsArr[2].ToUpper();
            if (Enum.GetNames(typeof(SiegeRace)).Contains(raceName))
            {
                sr = Enum.Parse<SiegeRace>(raceName);
            }
            else
            {
                if (int.TryParse(paramsArr[2], out int legionId))
                {
                    legion = LegionService.GetInstance().GetLegion(legionId);
                }
                else
                {
                    string legionName = "";
                    for (int i = 2; i < paramsArr.Length; i++)
                        legionName += " " + paramsArr[i];
                    legion = LegionService.GetInstance().GetLegion(legionName.Trim());
                }
                if (legion != null)
                {
                    sr = SiegeRaceExtensions.GetByRace(PlayerService.GetOrLoadPlayerCommonData(legion.GetBrigadeGeneral().GetObjectId()).GetRace());
                }
            }
            if (legion == null && sr == null)
            {
                SendInfo(player, paramsArr[2] + " is not valid race or legion");
                return;
            }
        }
        else
        {
            sr = SiegeRaceExtensions.GetByRace(player.GetRace());
        }
        SiegeService.GetInstance().CaptureSiege(sr.Value, legion != null ? legion.GetLegionId() : 0, loc.GetLocationId());
    }

    private void Assault(Player player, SiegeLocation loc, int delaySeconds)
    {
        if (BalaurAssaultService.GetInstance().StartAssault(loc.GetLocationId(), delaySeconds))
            SendInfo(player, "Started assault on " + GetLocationName(loc));
        else
        {
            if (SiegeService.GetInstance().IsSiegeInProgress(loc.GetLocationId()))
                SendInfo(player, "Assault on " + GetLocationName(loc) + " was already started.");
            else
                SendInfo(player, GetLocationName(loc) + " must be under siege in order to start an assault.");
        }
    }

    private SiegeLocation ParseLocation(string[] paramsArr)
    {
        SiegeLocation location = paramsArr.Length < 2 ? null : SiegeService.GetInstance().GetSiegeLocation(int.Parse(paramsArr[1]));
        if (location == null)
            throw new ArgumentException("Invalid locationId.");
        return location;
    }

    private static object GetLocationName(SiegeLocation loc)
    {
        return loc.GetTemplate().GetL10nId() == 0 ? loc.GetType_().ToString() + " " + loc.GetLocationId() : loc.GetTemplate().GetL10n();
    }
}
