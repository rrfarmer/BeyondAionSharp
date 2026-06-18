using System;
using System.Linq;
using Aion.GameServer.Model.Base;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/BaseCommand.</summary>
public class BaseCommand : AdminCommand
{
    public BaseCommand()
        : base("base", "Lists bases or changes their state.")
    {
        SetSyntaxInfo(
            "list - Lists all available base locations with their respective occupier.",
            "start <id> - Activates the specified base.",
            "stop <id> - Deactivates the specified base.",
            "capture <id> <occupier> - Captures the specified base with the specified new occupier.",
            "assault <id> - Spawns attacker NPCs for the specified base if available."
        );
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        switch (paramsArr[0].ToLower())
        {
            case "list":
                ShowBaseLocationList(player);
                break;
            case "start":
                StartBase(player, paramsArr);
                break;
            case "stop":
                StopBase(player, paramsArr);
                break;
            case "capture":
                CaptureBase(player, paramsArr);
                break;
            case "assault":
                AssaultBase(player, paramsArr);
                break;
            default:
                SendInfo(player);
                break;
        }
    }

    private void ShowBaseLocationList(Player player)
    {
        BaseService.GetInstance().GetBaseLocations().OrderBy(loc => loc.GetId())
            .ToList()
            .ForEach(loc => SendInfo(player, "Base " + loc.GetId() + " belongs to " + loc.GetOccupier()));
    }

    private void StartBase(Player player, string[] paramsArr)
    {
        int baseId = ParseBaseId(player, paramsArr, 2);
        if (baseId == 0)
            return;

        if (BaseService.GetInstance().IsActive(baseId))
        {
            SendInfo(player, "Base is already active");
            return;
        }
        BaseService.GetInstance().Start(baseId);
    }

    private void StopBase(Player player, string[] paramsArr)
    {
        int baseId = ParseBaseId(player, paramsArr, 2);
        if (baseId == 0)
            return;

        if (!BaseService.GetInstance().IsActive(baseId))
        {
            SendInfo(player, "Base is already inactive.");
            return;
        }
        BaseService.GetInstance().Stop(baseId);
    }

    protected void CaptureBase(Player player, string[] paramsArr)
    {
        int baseId = ParseBaseId(player, paramsArr, 3);
        if (baseId == 0)
            return;

        if (!BaseService.GetInstance().IsActive(baseId))
        {
            SendInfo(player, "Inactive bases cannot be captured.");
            return;
        }

        BaseOccupier occupier = Enum.Parse<BaseOccupier>(paramsArr[2].ToUpper());
        BaseService.GetInstance().Capture(baseId, occupier);
    }

    protected void AssaultBase(Player player, string[] paramsArr)
    {
        int baseId = ParseBaseId(player, paramsArr, 3);
        if (baseId == 0)
            return;

        Base baseLoc = BaseService.GetInstance().GetActiveBase(baseId);
        if (baseLoc == null)
        {
            SendInfo(player, "Inactive bases cannot be assaulted.");
            return;
        }
        if (baseLoc.IsUnderAssault())
        {
            SendInfo(player, "Base is already under assault.");
            return;
        }
        BaseOccupier occupier = Enum.Parse<BaseOccupier>(paramsArr[2].ToUpper());
        if (baseLoc.GetOccupier() == occupier)
        {
            SendInfo(player, "Base cannot be assaulted by the same occupier");
            return;
        }
        baseLoc.SpawnBySpawnHandler(SpawnHandlerType.ATTACKER, occupier);
    }

    private int ParseBaseId(Player admin, string[] paramsArr, int expectedParameters)
    {
        if (paramsArr.Length < expectedParameters)
        {
            SendInfo(admin, "Not enough parameters.");
            return 0;
        }

        int baseId = int.Parse(paramsArr[1]);
        if (BaseService.GetInstance().GetBaseLocation(baseId) == null)
        {
            SendInfo(admin, "Invalid base ID.");
            return 0;
        }

        return baseId;
    }
}
