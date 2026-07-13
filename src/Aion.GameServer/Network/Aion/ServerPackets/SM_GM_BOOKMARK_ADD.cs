using Aion.GameServer.Dao;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Adds a persisted GM teleport bookmark to the client.</summary>
public class SM_GM_BOOKMARK_ADD : AionServerPacket
{
    private readonly BookmarkDAO.Bookmark bookmark;

    public SM_GM_BOOKMARK_ADD(BookmarkDAO.Bookmark bookmark)
    {
        this.bookmark = bookmark;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS(bookmark.Name);
        WriteD(bookmark.WorldId);
        WriteF(bookmark.X);
        WriteF(bookmark.Y);
        WriteF(bookmark.Z);
    }
}
