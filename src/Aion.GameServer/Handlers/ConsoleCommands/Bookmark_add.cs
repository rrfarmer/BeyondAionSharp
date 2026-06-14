using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Bookmark_add (ginho1). Adds a GM teleport bookmark at the admin's current position.</summary>
public class Bookmark_add : ConsoleCommand
{
    public Bookmark_add()
        : base("Bookmark_add")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length > 0)
        {
            string bookmark_name = "";
            foreach (string str in paramsArr)
            {
                bookmark_name += str + " ";
            }
            if (bookmark_name.Length != 0)
            {
                bookmark_name = bookmark_name.Trim();
                PacketSendUtility.SendPacket(admin, new SM_GM_BOOKMARK_ADD(bookmark_name, admin.GetWorldId(), admin.GetX(), admin.GetY(), admin.GetZ()));
            }
        }
    }
}
