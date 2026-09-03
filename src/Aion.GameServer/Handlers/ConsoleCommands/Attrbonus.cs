using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Attrbonus (ginho1). Modifies your stats via the Stat admin command.</summary>
public class Attrbonus : ConsoleCommand
{
    public Attrbonus()
        : base("attrbonus", "Modifies your stats.")
    {
        SetSyntaxInfo(
            "list - Lists all stats.",
            "<stat> - Shows active stat functions for the given stat.",
            "<stat> <value> - Sets the given stat to the given value.",
            "cancel - Cancels all active stat overrides.",
            "Stat parameters accept lowercase and abbreviated formats, such as flytime or flyt instead of FLY_TIME.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        Aion.GameServer.Handlers.AdminCommands.Stat statCommand = ChatProcessor.GetInstance().GetCommand<Aion.GameServer.Handlers.AdminCommands.Stat>();
        if (paramsArr.Length == 1 && "list".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            statCommand.ListStats(admin);
        }
        else if (paramsArr.Length == 1 && "cancel".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            statCommand.CancelStatOverrides(admin, admin);
        }
        else if (paramsArr.Length == 1)
        {
            statCommand.ShowStatFunctions(admin, admin, paramsArr[0]);
        }
        else if (paramsArr.Length == 2)
        {
            statCommand.SetStat(admin, admin, paramsArr[0], Aion.GameServer.Utils.ChatHandlers.JavaNumberParser.ParseInt(paramsArr[1]));
        }
        else
        {
            SendInfo(admin);
        }
    }
}
