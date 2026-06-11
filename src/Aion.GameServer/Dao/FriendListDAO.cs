using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Services.Player;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/FriendListDAO (@author Ben). JDBC DAO over friends. MIXED: load via DatabaseFactory; add/del/setMemo via the commons
/// DB callback helper (DB.InsertUpdate(IUStH), anonymous->nested). The add/del handlers do bidirectional inserts/deletes: Java's two
/// addBatch()+executeBatch() on one PreparedStatement -> two sequential ExecuteNonQuery with Parameters.Clear between (same two-row effect;
/// DB.InsertUpdate hands the handler a plain MySqlCommand, not a MySqlBatch). rset.next()/getInt/getString->Read()/GetInt32/GetString(GetOrdinal).
/// Friend(pcd, memo); FriendList(player, friends); PlayerService.GetOrLoadPlayerCommonData. SQL verbatim.
/// </summary>
public class FriendListDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(FriendListDAO));

    public const string LOAD_QUERY = "SELECT * FROM `friends` WHERE `player`=?";
    public const string ADD_QUERY = "INSERT INTO `friends` (`player`,`friend`) VALUES (?, ?)";
    public const string DEL_QUERY = "DELETE FROM friends WHERE player = ? AND friend = ?";
    public const string SET_MEMO_QUERY = "UPDATE friends SET memo=? WHERE player=? AND friend=?";

    public static FriendList Load(Player player)
    {
        List<Friend> friends = new List<Friend>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = LOAD_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            using MySqlDataReader rset = stmt.ExecuteReader();
            while (rset.Read())
            {
                int objId = rset.GetInt32(rset.GetOrdinal("friend"));
                PlayerCommonData pcd = PlayerService.GetOrLoadPlayerCommonData(objId);
                if (pcd != null)
                {
                    Friend friend = new Friend(pcd, rset.GetString(rset.GetOrdinal("memo")));
                    friends.Add(friend);
                }
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not restore FriendList data for player: " + player.GetObjectId() + " from DB: " + e.Message);
        }

        return new FriendList(player, friends);
    }

    public static bool AddFriends(Player player, Player friend)
    {
        return DB.InsertUpdate(ADD_QUERY, new AddFriendsHandler(player, friend));
    }

    private sealed class AddFriendsHandler : IUStH
    {
        private readonly Player player;
        private readonly Player friend;

        internal AddFriendsHandler(Player player, Player friend)
        {
            this.player = player;
            this.friend = friend;
        }

        public void HandleInsertUpdate(MySqlCommand ps)
        {
            ps.Parameters.Clear();
            ps.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            ps.Parameters.Add(new MySqlParameter { Value = friend.GetObjectId() });
            ps.ExecuteNonQuery();

            ps.Parameters.Clear();
            ps.Parameters.Add(new MySqlParameter { Value = friend.GetObjectId() });
            ps.Parameters.Add(new MySqlParameter { Value = player.GetObjectId() });
            ps.ExecuteNonQuery();
        }
    }

    public static bool DelFriends(int playerOid, int friendOid)
    {
        return DB.InsertUpdate(DEL_QUERY, new DelFriendsHandler(playerOid, friendOid));
    }

    private sealed class DelFriendsHandler : IUStH
    {
        private readonly int playerOid;
        private readonly int friendOid;

        internal DelFriendsHandler(int playerOid, int friendOid)
        {
            this.playerOid = playerOid;
            this.friendOid = friendOid;
        }

        public void HandleInsertUpdate(MySqlCommand ps)
        {
            ps.Parameters.Clear();
            ps.Parameters.Add(new MySqlParameter { Value = playerOid });
            ps.Parameters.Add(new MySqlParameter { Value = friendOid });
            ps.ExecuteNonQuery();

            ps.Parameters.Clear();
            ps.Parameters.Add(new MySqlParameter { Value = friendOid });
            ps.Parameters.Add(new MySqlParameter { Value = playerOid });
            ps.ExecuteNonQuery();
        }
    }

    public static bool SetFriendMemo(int playerOid, int friendOid, string memo)
    {
        return DB.InsertUpdate(SET_MEMO_QUERY, new SetFriendMemoHandler(playerOid, friendOid, memo));
    }

    private sealed class SetFriendMemoHandler : IUStH
    {
        private readonly int playerOid;
        private readonly int friendOid;
        private readonly string memo;

        internal SetFriendMemoHandler(int playerOid, int friendOid, string memo)
        {
            this.playerOid = playerOid;
            this.friendOid = friendOid;
            this.memo = memo;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = memo });
            stmt.Parameters.Add(new MySqlParameter { Value = playerOid });
            stmt.Parameters.Add(new MySqlParameter { Value = friendOid });
            stmt.ExecuteNonQuery();
        }
    }
}
