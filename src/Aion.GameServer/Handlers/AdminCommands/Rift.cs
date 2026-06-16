using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Rift.</summary>
public class Rift : AdminCommand
{
    private const string COMMAND_OPEN = "open";
    private const string COMMAND_CLOSE = "close";

    public Rift()
        : base("rift")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            ShowHelp(player);
            return;
        }

        if (COMMAND_CLOSE.Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase) || COMMAND_OPEN.Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            HandleRift(player, paramsArr);
        }
    }

    protected void HandleRift(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 2 || !IsDigits(paramsArr[1]))
        {
            ShowHelp(player);
            return;
        }

        int id = int.TryParse(paramsArr[1], out var r) ? r : 0;
        bool result;
        if (!IsValidId(player, id))
        {
            ShowHelp(player);
            return;
        }

        if (COMMAND_OPEN.Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            bool guards = ParseBoolean(paramsArr[2]);
            result = RiftService.GetInstance().OpenRifts(id, guards).Succeeded;
            PacketSendUtility.SendMessage(player, result ? "Rifts is opened!" : "Rifts was already opened");
        }
        else if (COMMAND_CLOSE.Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            result = RiftService.GetInstance().CloseRifts(id).Succeeded;
            PacketSendUtility.SendMessage(player, result ? "Rifts is closed!" : "Rifts was already closed");
        }
    }

    protected bool IsValidId(Player player, int id)
    {
        if (!RiftService.GetInstance().IsValidId(id))
        {
            PacketSendUtility.SendMessage(player, "Id " + id + " is invalid");
            return false;
        }

        return true;
    }

    protected void ShowHelp(Player player)
    {
        PacketSendUtility.SendMessage(player, "AdminCommand //rift open|close <Id|worldId> (open with boolean for guards)");
    }

    // Java parity: org.apache.commons.lang3.math.NumberUtils.isDigits(String) — true if non-empty and all ASCII digits.
    private static bool IsDigits(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;
        foreach (char c in str)
            if (!char.IsDigit(c))
                return false;
        return true;
    }

    // Java parity: java.lang.Boolean.parseBoolean(String) — true iff string equals "true" ignoring case.
    private static bool ParseBoolean(string s)
    {
        return "true".Equals(s, StringComparison.OrdinalIgnoreCase);
    }
}
