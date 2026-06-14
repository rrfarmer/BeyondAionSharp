using System;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Classup (ginho1, Neon). Promotes the target player to an advanced class.</summary>
public class Classup : ConsoleCommand
{
    public Classup()
        : base("classup", "Promotes a players class.")
    {
        SetSyntaxInfo("<class> - Promotes your characters class to the one specified.");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(admin);
            return;
        }

        VisibleObject target = admin.GetTarget();
        if (target is not Player player)
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        string newClass = paramsArr[0];

        if (newClass.Equals("fighter", StringComparison.OrdinalIgnoreCase))
            newClass = "GLADIATOR";
        else if (newClass.Equals("knight", StringComparison.OrdinalIgnoreCase))
            newClass = "TEMPLAR";
        else if (newClass.Equals("wizard", StringComparison.OrdinalIgnoreCase))
            newClass = "SORCERER";
        else if (newClass.Equals("elementalist", StringComparison.OrdinalIgnoreCase))
            newClass = "SPIRIT_MASTER";

        // Java parity: PlayerClass.valueOf(newClass.toUpperCase()); if (playerClass.isStartingClass()) throw new IllegalArgumentException();
        if (!Enum.TryParse(newClass.ToUpper(), out PlayerClass playerClass) || !Enum.IsDefined(typeof(PlayerClass), playerClass) || playerClass.IsStartingClass())
        {
            SendInfo(admin, "Invalid player class.");
            return;
        }

        ClassChangeService.SetClass(player, playerClass, false, true);
        SendInfo(admin, "You have promoted " + player.GetName() + "'s class to " + playerClass.ToString().ToLower() + ".");
    }
}
