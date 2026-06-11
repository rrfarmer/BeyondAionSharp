using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Players;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/BlockListDAO (@author Ben). Saving/loading players' block lists. Uses the commons DB callback helper
/// (DB.InsertUpdate(query, IUStH) / DB.Select(query, ParamReadStH)). Anonymous IUStH/ParamReadStH -> private nested classes capturing
/// the locals via ctor. JDBC->ADO.NET inside handlers: setInt/setString -> Parameters.Add(new MySqlParameter{Value}); stmt.execute() ->
/// ExecuteNonQuery; rset.next()/getInt/getString -> reader.Read()/GetInt32(GetOrdinal)/GetString(GetOrdinal). Map.put -> indexer. SQL
/// verbatim. PlayerService.GetPlayerName red-tolerated.
/// </summary>
public class BlockListDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(BlockListDAO));

    public const string LOAD_QUERY = "SELECT blocked_player, reason FROM blocks WHERE player=?";
    public const string ADD_QUERY = "INSERT INTO blocks (player, blocked_player, reason) VALUES (?, ?, ?)";
    public const string DEL_QUERY = "DELETE FROM blocks WHERE player=? AND blocked_player=?";
    public const string SET_REASON_QUERY = "UPDATE blocks SET reason=? WHERE player=? AND blocked_player=?";

    public static bool AddBlockedUser(int playerObjId, int objIdToBlock, string reason)
    {
        return DB.InsertUpdate(ADD_QUERY, new AddBlockedUserHandler(playerObjId, objIdToBlock, reason));
    }

    private sealed class AddBlockedUserHandler : IUStH
    {
        private readonly int playerObjId;
        private readonly int objIdToBlock;
        private readonly string reason;

        internal AddBlockedUserHandler(int playerObjId, int objIdToBlock, string reason)
        {
            this.playerObjId = playerObjId;
            this.objIdToBlock = objIdToBlock;
            this.reason = reason;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjId });
            stmt.Parameters.Add(new MySqlParameter { Value = objIdToBlock });
            stmt.Parameters.Add(new MySqlParameter { Value = reason });
            stmt.ExecuteNonQuery();
        }
    }

    public static bool DelBlockedUser(int playerObjId, int objIdToDelete)
    {
        return DB.InsertUpdate(DEL_QUERY, new DelBlockedUserHandler(playerObjId, objIdToDelete));
    }

    private sealed class DelBlockedUserHandler : IUStH
    {
        private readonly int playerObjId;
        private readonly int objIdToDelete;

        internal DelBlockedUserHandler(int playerObjId, int objIdToDelete)
        {
            this.playerObjId = playerObjId;
            this.objIdToDelete = objIdToDelete;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjId });
            stmt.Parameters.Add(new MySqlParameter { Value = objIdToDelete });
            stmt.ExecuteNonQuery();
        }
    }

    public static BlockList Load(int playerObjId)
    {
        Dictionary<int, BlockedPlayer> list = new Dictionary<int, BlockedPlayer>();

        DB.Select(LOAD_QUERY, new LoadHandler(playerObjId, list));
        return new BlockList(list);
    }

    private sealed class LoadHandler : ParamReadStH
    {
        private readonly int playerObjId;
        private readonly Dictionary<int, BlockedPlayer> list;

        internal LoadHandler(int playerObjId, Dictionary<int, BlockedPlayer> list)
        {
            this.playerObjId = playerObjId;
            this.list = list;
        }

        public void HandleRead(MySqlDataReader rset)
        {
            while (rset.Read())
            {
                int blockedOid = rset.GetInt32(rset.GetOrdinal("blocked_player"));
                string name = PlayerService.GetPlayerName(blockedOid);
                if (name == null)
                {
                    log.LogError("Attempt to load block list for player " + playerObjId + " tried to load a player which does not exist: " + blockedOid);
                }
                else
                {
                    list[blockedOid] = new BlockedPlayer(blockedOid, name, rset.GetString(rset.GetOrdinal("reason")));
                }
            }
        }

        public void SetParams(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjId });
        }
    }

    public static bool SetReason(int playerObjId, int blockedPlayerObjId, string reason)
    {
        return DB.InsertUpdate(SET_REASON_QUERY, new SetReasonHandler(playerObjId, blockedPlayerObjId, reason));
    }

    private sealed class SetReasonHandler : IUStH
    {
        private readonly int playerObjId;
        private readonly int blockedPlayerObjId;
        private readonly string reason;

        internal SetReasonHandler(int playerObjId, int blockedPlayerObjId, string reason)
        {
            this.playerObjId = playerObjId;
            this.blockedPlayerObjId = blockedPlayerObjId;
            this.reason = reason;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = reason });
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjId });
            stmt.Parameters.Add(new MySqlParameter { Value = blockedPlayerObjId });
            stmt.ExecuteNonQuery();
        }
    }
}
