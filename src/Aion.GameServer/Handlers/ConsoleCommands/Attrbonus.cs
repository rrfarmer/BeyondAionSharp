using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Attrbonus (ginho1). Sets a stat on the admin to a fixed value.</summary>
public class Attrbonus : ConsoleCommand, IStatOwner
{
    public Attrbonus()
        : base("attrbonus")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            Info(admin, null);
            return;
        }

        // Java parity: StatEnum.valueOf(name) throws IllegalArgumentException on invalid (the original `if (stat == null)` is dead code);
        // reproduced as TryParse -> the author's intended "Invalid params." message.
        if (!System.Enum.TryParse(paramsArr[0], out StatEnum stat) || !System.Enum.IsDefined(typeof(StatEnum), stat))
        {
            PacketSendUtility.SendMessage(admin, "Invalid params.");
            return;
        }

        // Java parity: Integer.parseInt(params[1]) throws NumberFormatException -> "Invalid params."
        if (!int.TryParse(paramsArr[1], out int value))
        {
            PacketSendUtility.SendMessage(admin, "Invalid params.");
            return;
        }

        Creature effected = admin;
        CreatureGameStats cgs = effected.GetGameStats();

        List<IStatFunction> modifiers = new List<IStatFunction>();

        modifiers.Add(new StatSetFunction(stat, value));

        if (modifiers.Count > 0)
            cgs.AddEffect(this, modifiers);

        PacketSendUtility.SendMessage(admin, "Character stat " + stat.ToString() + " increased.");
    }

    // Java parity: Attrbonus.info(Player, String) — ChatCommand has no info() in the C# port, so this is a private helper.
    private void Info(Player admin, string message)
    {
        PacketSendUtility.SendMessage(admin, "syntax ///attrbonus <stat> <value>");
    }
}
