using Aion.GameServer.Dao;
using Aion.GameServer.Handlers.ConsoleCommands;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

public class Bookmark : AdminCommand
{
    public Bookmark()
        : base("bookmark", "Manages teleport bookmarks.")
    {
        SetSyntaxInfo(
            "del <name> - Deletes the bookmark with the specified name.",
            "deleteAll - Deletes all bookmarks.",
            "Note: Press Shift+G and click the \"Bookmark\" button to add or use your teleport bookmarks.");
    }

    protected override string GetAliasForLevel()
    {
        return Bookmark_add.ALIAS;
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length >= 2 && paramsArr[0].Equals("del", StringComparison.OrdinalIgnoreCase))
        {
            string bookmarkName = string.Join(" ", paramsArr).Substring(4);
            bool deleted = BookmarkDAO.DeleteBookmark(player.GetObjectId(), bookmarkName);
            SendInfo(player, deleted
                ? "The bookmark has been deleted. The bookmarks list will be updated after relog."
                : "No bookmark with that name was found.");
        }
        else if (paramsArr.Length >= 1 && paramsArr[0].Equals("deleteAll", StringComparison.OrdinalIgnoreCase))
        {
            BookmarkDAO.DeleteAll(player.GetObjectId());
            SendInfo(player, "All bookmarks have been deleted. The bookmarks list will be updated after relog.");
        }
        else
        {
            SendInfo(player);
        }
    }
}
