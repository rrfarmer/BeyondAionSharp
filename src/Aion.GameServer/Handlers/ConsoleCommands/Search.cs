using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Search (ginho1). Opens the GM search window for a named player.</summary>
public class Search : ConsoleCommand
{
    public Search()
        : base("search")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length > 0)
        {
            Player p = World.World.GetInstance().GetPlayer(paramsArr[0]);
            if (p != null)
                PacketSendUtility.SendPacket(admin, new SM_GM_SEARCH(p));
        }
    }
}
