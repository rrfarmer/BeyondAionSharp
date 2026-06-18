using Aion.GameServer.Handlers.AdminCommands;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>
/// Java parity: data/handlers/consolecommands/Teleportto (Neon).
/// This command gets executed when clicking the "Transport" button in first tab of the GM console.
/// Functionally identical to //goto. Typing help or " " in the input field will show the syntax info or location list respectively.
/// </summary>
public class Teleportto : ConsoleCommand
{
    public Teleportto()
        : base("teleportto", "Teleports you to regions by name.")
    {
    }

    public override string GetSyntaxInfo()
    {
        GoTo goTo = ChatProcessor.GetInstance().GetCommand<GoTo>();
        return goTo.GetSyntaxInfo().Replace(goTo.GetAliasWithPrefix(), GetAliasWithPrefix());
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        ChatProcessor.GetInstance().GetCommand<GoTo>().Execute(admin, paramsArr);
    }
}
