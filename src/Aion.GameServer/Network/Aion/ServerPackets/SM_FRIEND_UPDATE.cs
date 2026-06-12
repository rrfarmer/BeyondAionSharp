using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Status = global::Aion.GameServer.Model.GameObjects.Players.FriendList.Status;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_FRIEND_UPDATE (Ben, Neon). Updates a player's status entry in a friendlist (name/level/class/gender/map/lastonline/note/status). FriendList.Status nested enum via alias; LoggerFactory -> NullLogger.</summary>
public class SM_FRIEND_UPDATE : AionServerPacket
{
    private int friendObjId;

    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(SM_FRIEND_UPDATE));

    public SM_FRIEND_UPDATE(int friendObjId)
    {
        this.friendObjId = friendObjId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Friend f = con.GetActivePlayer().GetFriendList().GetFriend(friendObjId);
        if (f == null)
            log.LogDebug("Attempted to update friend list status of " + friendObjId + " for " + con.GetActivePlayer().GetName()
                + " - object ID not found on friend list");
        else
        {
            WriteS(f.GetName());
            WriteD(f.GetLevel());
            WriteD(f.GetPlayerClass().GetClassId());
            WriteC(f.GetGender().GetGenderId());
            WriteD(f.GetMapId());
            WriteD(f.GetStatus() == Status.ONLINE ? 0 : f.GetLastOnlineEpochSeconds());
            WriteS(f.GetNote());
            WriteC(f.GetStatus().GetId());
        }
    }
}
