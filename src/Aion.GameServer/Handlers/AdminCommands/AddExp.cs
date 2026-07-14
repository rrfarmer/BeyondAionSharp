using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/AddExp (Wakizashi). Increases/decreases a player's experience points.</summary>
public class AddExp : AdminCommand
{
    public AddExp()
        : base("addexp", "Increases/decreases a players experience points.")
    {
        SetSyntaxInfo("<exp> - The experience points to add (may be negative).");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        Player target = admin;

        if (admin.GetTarget() is Player p)
            target = p;

        long exp;
        if (!TryParseLong(paramsArr[0], out exp))
        {
            SendInfo(admin, "Invalid <exp> (must be a number)");
            return;
        }

        long resultExp = Math.Max(0, target.GetCommonData().GetExp() + exp);
        target.GetCommonData().SetExp(resultExp);
        SendInfo(admin, "You added " + exp + " exp points to " + target.GetName() + ".");
    }
}
