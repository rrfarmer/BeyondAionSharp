using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Database;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Services;

/// <summary>
/// Java parity: services/DatabaseCleaningService (nrg, Neon). Deletes all data about inactive players.
/// Thread.threadId()→ManagedThreadId; IllegalState/Argument→InvalidOperation/Argument; LinkedHashSet→List w/ Contains-guard (insertion-ordered unique); System.out.printf→Console.Write; try-with-resources→using; record accessors player.legionId()→property. JDBC (Connection/Stmt/ResultSet)/JAXBUtil/dao pillars red-tolerated.
/// </summary>
public class DatabaseCleaningService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(DatabaseCleaningService));

    private DatabaseCleaningService()
    {
    }

    public static void DeletePlayersOnInactiveAccounts()
    {
        if (Thread.CurrentThread.ManagedThreadId != 1)
            throw new InvalidOperationException("DatabaseCleaningService can only be run from the main thread on server startup");

        if (CleaningConfig.MIN_ACCOUNT_INACTIVITY_DAYS <= 30)
            throw new ArgumentException("The configured days for database cleaning is too low. For safety reasons the service will only execute with periods over 30 days");

        List<PlayerDAO.PlayerAndLegionInfo> players = PlayerDAO.GetPlayersOnInactiveAccounts(ToMaxExp(CleaningConfig.MAX_DELETABLE_CHAR_LEVEL), CleaningConfig.MIN_ACCOUNT_INACTIVITY_DAYS);
        if (players.Count == 0)
        {
            log.LogInformation("Found no inactive accounts with characters level <={MaxLevel} to delete", CleaningConfig.MAX_DELETABLE_CHAR_LEVEL);
            return;
        }
        DeletePlayers(players);
        List<PlayerDAO.PlayerAndLegionInfo> remainingLegionMembers = DeleteEmptyLegions(players);
        MaintainBrigadeGenerals(remainingLegionMembers);
        AddLegionHistoryLeaveEntry(remainingLegionMembers);
        if (players.Count >= 500)
            OptimizeDatabaseTables(WithForeignKeyTables("players", "inventory"));
    }

    private static long ToMaxExp(int charLevel)
    {
        FileInfo xml = new FileInfo("./data/static_data/player_experience_table.xml"); // fast unmarshal to avoid loading whole static data before
        PlayerExperienceTable pxt = (PlayerExperienceTable)JAXBUtil.Deserialize(xml, typeof(PlayerExperienceTable), "./data/static_data/static_data.xsd");
        return pxt.GetStartExpForLevel(charLevel + 1) - 1;
    }

    private static void DeletePlayers(List<PlayerDAO.PlayerAndLegionInfo> players)
    {
        long startMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        log.LogInformation("Deleting {Count} characters level <={MaxLevel} from inactive accounts...", players.Count, CleaningConfig.MAX_DELETABLE_CHAR_LEVEL);
        for (int i = 0; i < players.Count; i++)
        {
            if (i % 20 == 0)
                Console.Write(string.Format("Progress: {0,4:F1}%\r", i * 100f / players.Count));
            PlayerService.DeletePlayerFromDB(players[i].PlayerId(), false);
        }
        log.LogInformation("Deleted characters and related data from database in {Seconds} seconds", (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startMillis) / 1000);
    }

    private static List<PlayerDAO.PlayerAndLegionInfo> DeleteEmptyLegions(List<PlayerDAO.PlayerAndLegionInfo> players)
    {
        List<PlayerDAO.PlayerAndLegionInfo> remainingLegionMembers = new List<PlayerDAO.PlayerAndLegionInfo>();
        HashSet<int> deleted = new HashSet<int>();
        foreach (PlayerDAO.PlayerAndLegionInfo player in players)
        {
            if (player.LegionId() == 0 || deleted.Contains(player.LegionId()))
                continue;
            if (LegionMemberDAO.LoadLegionMembers(player.LegionId()).Count == 0)
            {
                LegionService.DeleteLegionFromDB(player.LegionId());
                deleted.Add(player.LegionId());
            }
            else
            {
                remainingLegionMembers.Add(player);
            }
        }
        if (deleted.Count != 0)
            log.LogInformation("Deleted {Count} empty legions", deleted.Count);
        return remainingLegionMembers;
    }

    private static void MaintainBrigadeGenerals(List<PlayerDAO.PlayerAndLegionInfo> deletedLegionMembers)
    {
        foreach (PlayerDAO.PlayerAndLegionInfo deletedLegionMember in deletedLegionMembers)
        {
            if (deletedLegionMember.LegionRank() != LegionRank.BRIGADE_GENERAL)
                continue;
            List<int> legionMembers = LegionMemberDAO.LoadLegionMembers(deletedLegionMember.LegionId());
            if (legionMembers.Count == 0 || legionMembers.Contains(deletedLegionMember.PlayerId()))
                continue;
            int newBrigadeGeneralId = legionMembers.Count == 1 ? legionMembers[0] : 0;
            if (newBrigadeGeneralId != 0 && LegionMemberDAO.SetRank(newBrigadeGeneralId, LegionRank.BRIGADE_GENERAL))
            {
                string newBrigadeGeneralName = PlayerDAO.GetPlayerNameByObjId(newBrigadeGeneralId);
                log.LogInformation("Transferred brigade general of legion {LegionId} from deleted player {Name} to the only remaining member {NewName}", deletedLegionMember.LegionId(), deletedLegionMember.Name(), newBrigadeGeneralName);
                LegionDAO.InsertHistory(deletedLegionMember.LegionId(), LegionHistoryAction.APPOINTED, newBrigadeGeneralName, "");
            }
            else
            {
                log.LogWarning("Legion {LegionId} has no brigade general anymore", deletedLegionMember.LegionId());
            }
        }
    }

    private static void AddLegionHistoryLeaveEntry(List<PlayerDAO.PlayerAndLegionInfo> players)
    {
        foreach (PlayerDAO.PlayerAndLegionInfo player in players)
            LegionDAO.InsertHistory(player.LegionId(), LegionHistoryAction.KICK, player.Name(), "");
    }

    // table names cannot be used as parameters, so unfortunately we have to concat the sql query
    private static void OptimizeDatabaseTables(List<string> tables)
    {
        long startMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        log.LogInformation("Optimizing {Count} database tables: {Tables}", tables.Count, string.Join(", ", tables));
        try
        {
            using var con = DatabaseFactory.GetConnection();
            using var stmt = con.PrepareStatement("OPTIMIZE TABLE " + string.Join(",", tables));
            stmt.Execute();
            log.LogInformation("Optimized database tables in {Seconds} seconds", (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startMillis) / 1000);
        }
        catch (Exception e)
        {
            log.LogError(e, "Optimize table failed");
        }
    }

    /// <summary>baseTables plus all tables which have a foreign key referencing the primary key of one of the given baseTables.</summary>
    private static List<string> WithForeignKeyTables(params string[] baseTables)
    {
        // Java LinkedHashSet → insertion-ordered unique List (no C# ordered-set).
        List<string> tables = new List<string>();
        foreach (string table in baseTables)
        {
            if (!tables.Contains(table))
                tables.Add(table);
            try
            {
                using var con = DatabaseFactory.GetConnection();
                var importedKeys = con.GetMetaData().GetExportedKeys(con.GetCatalog(), null, table);
                while (importedKeys.Next())
                {
                    string fk = importedKeys.GetString("FKTABLE_NAME");
                    if (!tables.Contains(fk))
                        tables.Add(fk);
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Failed to collect tables on players table");
            }
        }
        return new List<string>(tables);
    }
}
