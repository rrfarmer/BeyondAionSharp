using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Represents a player's Friend list. Java parity: model/gameobjects/player/FriendList implements Iterable&lt;Friend&gt;.
/// </summary>
public class FriendList : IEnumerable<Friend>
{
    private readonly ConcurrentDictionary<int, Friend> friends = new ConcurrentDictionary<int, Friend>();
    private readonly Player player;
    private Status status = Status.OFFLINE;

    /// <summary>Constructs a friend list for the given player, with the given friends.</summary>
    public FriendList(Player owner, ICollection<Friend> friends)
    {
        foreach (Friend friend in friends)
            this.friends[friend.GetObjectId()] = friend;
        this.player = owner;
    }

    /// <summary>Gets the friend with this objId. Returns null if it is not our friend.</summary>
    public Friend GetFriend(int objId)
    {
        return friends.TryGetValue(objId, out Friend f) ? f : null;
    }

    /// <summary>Returns number of friends in list</summary>
    public int GetSize()
    {
        return friends.Count;
    }

    /// <summary>Adds the given friend to the list. To add a friend in the database, see PlayerService.</summary>
    public void AddFriend(Friend friend)
    {
        friends[friend.GetObjectId()] = friend;
    }

    /// <summary>Gets the Friend by this name</summary>
    public Friend GetFriend(string name)
    {
        foreach (Friend friend in friends.Values)
            if (friend.GetName().Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return friend;
        return null;
    }

    /// <summary>Deletes given friend from this friends list (only affects this player; sends update packet).</summary>
    public void DelFriend(int friendOid)
    {
        friends.TryRemove(friendOid, out _);
    }

    public bool IsFull()
    {
        return GetSize() >= Aion.GameServer.Configs.Main.CustomConfig.FRIENDLIST_SIZE;
    }

    /// <summary>Gets players status</summary>
    public Status GetStatus()
    {
        return status;
    }

    /// <summary>Sets the status of the player. Does not update friends.</summary>
    public void SetStatus(Status status, PlayerCommonData pcd)
    {
        Status previousStatus = this.status;
        this.status = status;

        foreach (Friend friend in friends.Values)
        {
            Player friendPlayer = Aion.GameServer.World.World.GetInstance().GetPlayer(friend.GetObjectId());
            if (friendPlayer == null)
                continue;

            friendPlayer.GetFriendList().GetFriend(pcd.GetPlayerObjId()).SetPCD(pcd);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(friendPlayer, new Aion.GameServer.Network.Aion.ServerPackets.SmFriendUpdate(player.GetObjectId()));

            if (previousStatus == Status.OFFLINE)
            {
                // Show LOGIN message
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(friendPlayer, new Aion.GameServer.Network.Aion.ServerPackets.SmFriendNotify(Aion.GameServer.Network.Aion.ServerPackets.SmFriendNotify.LOGIN, player.GetName()));
            }
            else if (status == Status.OFFLINE)
            {
                // Show LOGOUT message
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(friendPlayer, new Aion.GameServer.Network.Aion.ServerPackets.SmFriendNotify(Aion.GameServer.Network.Aion.ServerPackets.SmFriendNotify.LOGOUT, player.GetName()));
            }
        }
    }

    public IEnumerator<Friend> GetEnumerator()
    {
        return friends.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public enum Status : byte
    {
        /// <summary>User is offline or invisible</summary>
        OFFLINE = 0,
        /// <summary>User is online</summary>
        ONLINE = 1,
        /// <summary>User is away or busy</summary>
        AWAY = 3,
    }
}

/// <summary>Java parity: FriendList.Status.getId() / getByValue(byte).</summary>
public static class FriendListStatusExtensions
{
    public static byte GetId(this FriendList.Status status)
    {
        return (byte)status;
    }

    /// <summary>Gets the Status from its byte value. Returns null if out of range.</summary>
    public static FriendList.Status? GetByValue(byte value)
    {
        foreach (FriendList.Status stat in System.Enum.GetValues<FriendList.Status>())
            if (stat.GetId() == value)
                return stat;
        return null;
    }
}
