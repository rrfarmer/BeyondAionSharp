using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>
/// Handles activities related to social groups ingame such as the buddy list, block list, etc.
/// Java parity: services/SocialService (Ben, Neon).
/// </summary>
public class SocialService
{
    public static bool AddBlockedUser(Player player, PlayerCommonData blockedPlayer, string reason)
    {
        if (BlockListDAO.AddBlockedUser(player.GetObjectId(), blockedPlayer.GetPlayerObjId(), reason))
        {
            player.GetBlockList().Add(new BlockedPlayer(blockedPlayer.GetPlayerObjId(), blockedPlayer.GetName(), reason));
            PacketSendUtility.SendPacket(player, new SmBlockList());
            PacketSendUtility.SendPacket(player, new SmBlockResponse(SmBlockResponse.BlockSuccessful, blockedPlayer.GetName()));
            return true;
        }
        return false;
    }

    public static bool DeleteBlockedUser(Player player, BlockedPlayer target)
    {
        if (BlockListDAO.DelBlockedUser(player.GetObjectId(), target.GetObjId()))
        {
            player.GetBlockList().Remove(target.GetObjId());
            PacketSendUtility.SendPacket(player, new SmBlockList());
            PacketSendUtility.SendPacket(player, new SmBlockResponse(SmBlockResponse.UnblockSuccessful, target.GetName()));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Sets the reason for blocking a user.
    /// </summary>
    /// <returns>True on success - May be false if the reason was the same and therefore not edited.</returns>
    public static bool SetBlockedReason(Player player, BlockedPlayer target, string reason)
    {
        if (!target.GetReason().Equals(reason))
        {
            if (BlockListDAO.SetReason(player.GetObjectId(), target.GetObjId(), reason))
            {
                target.SetReason(reason);
                PacketSendUtility.SendPacket(player, new SmBlockList());
                PacketSendUtility.SendPacket(player, new SmBlockResponse(SmBlockResponse.EditNote, target.GetName()));
                return true;
            }
        }
        return false;
    }

    /// <returns>True on success - May be false if the memo was the same and therefore not edited.</returns>
    public static bool SetFriendMemo(Player player, Friend target, string memo)
    {
        if (!target.GetFriendMemo().Equals(memo))
        {
            if (FriendListDAO.SetFriendMemo(player.GetObjectId(), target.GetObjectId(), memo))
            {
                target.SetFriendMemo(memo);
                PacketSendUtility.SendPacket(player, new SmFriendList());
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Adds two players to each others friend lists, and updates the database.
    /// </summary>
    /// <returns>True on success.</returns>
    public static bool MakeFriends(Player friend1, Player friend2)
    {
        if (friend1.GetFriendList().GetFriend(friend2.GetObjectId()) != null)
            return false;
        if (FriendListDAO.AddFriends(friend1, friend2))
        {
            friend1.GetFriendList().AddFriend(new Friend(friend2.GetCommonData(), ""));
            friend2.GetFriendList().AddFriend(new Friend(friend1.GetCommonData(), ""));

            PacketSendUtility.SendPacket(friend1, new SmFriendList());
            PacketSendUtility.SendPacket(friend2, new SmFriendList());

            PacketSendUtility.SendPacket(friend1, new SmFriendResponse(SmFriendResponse.TargetAdded, friend2.GetName()));
            PacketSendUtility.SendPacket(friend2, new SmFriendResponse(SmFriendResponse.TargetAdded, friend1.GetName()));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Deletes two players from eachother's friend lists, and updates the database.
    /// </summary>
    /// <returns>True on success.</returns>
    public static bool DeleteFriend(Player deleter, Friend friend)
    {
        int friendObjId = friend.GetObjectId();
        if (FriendListDAO.DelFriends(deleter.GetObjectId(), friendObjId))
        {
            Player friendPlayer = Aion.GameServer.World.World.GetInstance().GetPlayer(friendObjId);
            if (friendPlayer != null)
            {
                friendPlayer.GetFriendList().DelFriend(deleter.GetObjectId());
                PacketSendUtility.SendPacket(friendPlayer, new SmFriendList());
                PacketSendUtility.SendPacket(friendPlayer, new SmFriendNotify(SmFriendNotify.Deleted, deleter.GetName()));
            }
            // Delete from deleter's friend list and send packets
            deleter.GetFriendList().DelFriend(friendObjId);
            PacketSendUtility.SendPacket(deleter, new SmFriendList());
            PacketSendUtility.SendPacket(deleter, new SmFriendResponse(SmFriendResponse.TargetRemoved, friend.GetName()));
            return true;
        }
        return false;
    }
}
