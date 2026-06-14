using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Partyrecall (ginho1). Not implemented (parity stub).</summary>
public class Partyrecall : ConsoleCommand
{
    public Partyrecall()
        : base("partyrecall")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        PacketSendUtility.SendMessage(admin, "Command not implemented.");
        return;
    }
}
