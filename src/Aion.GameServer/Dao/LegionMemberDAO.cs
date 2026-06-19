using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/LegionMemberDAO (@author Simple). Stores/loads legion-member data. MIXED: uses the low-level DB helper
/// (DB.PrepareStatement/Close/ExecuteUpdateAndClose), DB.InsertUpdate(IUStH) and DatabaseFactory. Anonymous IUStH -> nested classes.
/// PreparedStatement->MySqlCommand; executeQuery->ExecuteReader; rs.next()/getInt/getString->Read()/GetInt32/GetString(GetOrdinal);
/// executeUpdate->ExecuteNonQuery. LegionRank.valueOf->Enum.Parse, getRank().toString()->GetRank().ToString(). loadLegionMembers wraps
/// SQLException in a RuntimeException -> throw new Exception. LegionService.GetInstance().GetLegion. SQL verbatim.
/// </summary>
public class LegionMemberDAO
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(LegionMemberDAO));

    private const string INSERT_LEGIONMEMBER_QUERY = "INSERT INTO legion_members(`legion_id`, `player_id`, `rank`) VALUES (?, ?, ?)";
    private const string UPDATE_LEGIONMEMBER_QUERY = "UPDATE legion_members SET nickname=?, `rank`=?, selfintro=?, challenge_score=? WHERE player_id=?";
    private const string UPDATE_RANK_QUERY = "UPDATE legion_members SET `rank`=? WHERE player_id=?";
    private const string SELECT_LEGIONMEMBER_QUERY = "SELECT * FROM legion_members WHERE player_id = ?";
    private const string DELETE_LEGIONMEMBER_QUERY = "DELETE FROM legion_members WHERE player_id = ?";
    private const string SELECT_LEGIONMEMBERS_QUERY = "SELECT player_id FROM legion_members WHERE legion_id = ?";

    public static bool IsIdUsed(int playerObjId)
    {
        MySqlCommand s = DB.PrepareStatement("SELECT count(player_id) as cnt FROM legion_members WHERE ? = legion_members.player_id");
        try
        {
            s.Parameters.Add(new MySqlParameter { Value = playerObjId });
            using MySqlDataReader rs = s.ExecuteReader();
            rs.Read();
            return rs.GetInt32(rs.GetOrdinal("cnt")) > 0;
        }
        catch (Exception e)
        {
            log.LogError(e, "Can't check if name " + playerObjId + ", is used, returning possitive result");
            return true;
        }
        finally
        {
            DB.Close(s);
        }
    }

    public static bool SaveNewLegionMember(LegionMember legionMember)
    {
        bool success = DB.InsertUpdate(INSERT_LEGIONMEMBER_QUERY, new SaveNewHandler(legionMember));
        return success;
    }

    private sealed class SaveNewHandler : IUStH
    {
        private readonly LegionMember legionMember;

        internal SaveNewHandler(LegionMember legionMember)
        {
            this.legionMember = legionMember;
        }

        public void HandleInsertUpdate(MySqlCommand preparedStatement)
        {
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionMember.GetLegion().GetLegionId() });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionMember.GetObjectId() });
            preparedStatement.Parameters.Add(new MySqlParameter { Value = legionMember.GetRank().ToString() });
            preparedStatement.ExecuteNonQuery();
        }
    }

    public static void StoreLegionMember(LegionMember legionMember)
    {
        DB.InsertUpdate(UPDATE_LEGIONMEMBER_QUERY, new StoreHandler(legionMember));
    }

    private sealed class StoreHandler : IUStH
    {
        private readonly LegionMember legionMember;

        internal StoreHandler(LegionMember legionMember)
        {
            this.legionMember = legionMember;
        }

        public void HandleInsertUpdate(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = legionMember.GetNickname() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionMember.GetRank().ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionMember.GetSelfIntro() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionMember.GetChallengeScore() });
            stmt.Parameters.Add(new MySqlParameter { Value = legionMember.GetObjectId() });
            stmt.ExecuteNonQuery();
        }
    }

    public static LegionMember LoadLegionMember(int playerObjId)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_LEGIONMEMBER_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = playerObjId });
            using MySqlDataReader resultSet = stmt.ExecuteReader();
            if (!resultSet.Read())
                return null;
            int legionId = resultSet.GetInt32(resultSet.GetOrdinal("legion_id"));
            Legion legion = LegionService.GetInstance().GetLegion(legionId);
            if (legion == null) // disbanded by calling getLegion
                return null;
            LegionMember legionMember = new LegionMember(playerObjId, legion);
            legionMember.SetRank(Enum.Parse<LegionRank>(resultSet.GetString(resultSet.GetOrdinal("rank"))));
            legionMember.SetNickname(resultSet.GetString(resultSet.GetOrdinal("nickname")));
            int siOrd = resultSet.GetOrdinal("selfintro"); // selfintro is nullable (varchar DEFAULT '', no NOT NULL); Java getString tolerates null, C# GetString throws on DBNull
            legionMember.SetSelfIntro(resultSet.IsDBNull(siOrd) ? null : resultSet.GetString(siOrd));
            legionMember.SetChallengeScore(resultSet.GetInt32(resultSet.GetOrdinal("challenge_score")));
            return legionMember;
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not load legion member " + playerObjId);
            return null;
        }
    }

    public static List<int> LoadLegionMembers(int legionId)
    {
        List<int> legionMembers = new List<int>();
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = SELECT_LEGIONMEMBERS_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = legionId });
            using MySqlDataReader rs = stmt.ExecuteReader();
            while (rs.Read())
                legionMembers.Add(rs.GetInt32(rs.GetOrdinal("player_id")));
        }
        catch (Exception e)
        {
            throw new Exception("Could not load members of legion " + legionId, e);
        }
        return legionMembers;
    }

    public static void DeleteLegionMember(int playerObjId)
    {
        MySqlCommand statement = DB.PrepareStatement(DELETE_LEGIONMEMBER_QUERY);
        try
        {
            statement.Parameters.Add(new MySqlParameter { Value = playerObjId });
        }
        catch (Exception e)
        {
            log.LogError(e, "Some crap, can't set int parameter to PreparedStatement");
        }
        DB.ExecuteUpdateAndClose(statement);
    }

    public static bool SetRank(int playerId, LegionRank legionRank)
    {
        try
        {
            using MySqlConnection con = DatabaseFactory.GetConnection();
            con.Open();
            using MySqlCommand stmt = con.CreateCommand();
            stmt.CommandText = UPDATE_RANK_QUERY;
            stmt.Parameters.Add(new MySqlParameter { Value = legionRank.ToString() });
            stmt.Parameters.Add(new MySqlParameter { Value = playerId });
            return stmt.ExecuteNonQuery() > 0;
        }
        catch (Exception e)
        {
            log.LogError(e, "Could not set rank of player {PlayerId} to {LegionRank}", playerId, legionRank);
            return false;
        }
    }
}
