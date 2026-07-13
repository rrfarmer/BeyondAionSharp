using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Bookmark_add (ginho1). Adds a GM teleport bookmark at the admin's current position.</summary>
public class Bookmark_add : ConsoleCommand
{
    public const string ALIAS = "Bookmark_add";

    public Bookmark_add()
        : base(ALIAS)
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        string bookmarkName = string.Join(" ", paramsArr);
        if (bookmarkName.Length == 0)
            return;
        if (bookmarkName.Length > 27)
            bookmarkName = bookmarkName.Substring(0, 27);
        var bookmark = new BookmarkDAO.Bookmark(bookmarkName, admin.GetWorldId(), admin.GetX(), admin.GetY(), admin.GetZ());
        BookmarkDAO.StoreBookmark(admin.GetObjectId(), bookmark);
        PacketSendUtility.SendPacket(admin, new SM_GM_BOOKMARK_ADD(bookmark));
    }
}
