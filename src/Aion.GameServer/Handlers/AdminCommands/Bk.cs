using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Bk (Mrakobes, antness).</summary>
/// <remarks>
/// The Java class overrides ChatCommand.info(Player, String); that base method is a throw-only "don't call/override me"
/// hook never invoked by the framework, so the override is dead code with no observable effect and the C# base exposes no
/// equivalent virtual. It is intentionally omitted; all behavior here goes through PacketSendUtility.SendMessage exactly as in Java.
/// </remarks>
public class Bk : AdminCommand
{
    List<Bookmark> bookmarks = new List<Bookmark>();
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(Bk));
    private string bookmark_name = "";

    public Bk()
        : base("bk")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            PacketSendUtility.SendMessage(player, "syntax //bk <add|del|tele|list>");
            return;
        }

        if (paramsArr[0].Equals("add"))
            try
            {
                bookmark_name = paramsArr[1].ToLower();
                if (IsBookmarkExists(bookmark_name, player.GetObjectId()))
                {
                    PacketSendUtility.SendMessage(player, "Bookmark " + bookmark_name + " already exists !");
                    return;
                }

                float x = player.GetX();
                float y = player.GetY();
                float z = player.GetZ();
                int char_id = player.GetObjectId();
                int world_id = player.GetWorldId();

                DB.InsertUpdate("INSERT INTO bookmark (" + "`name`,`char_id`, `x`, `y`, `z`,`world_id` )" + " VALUES " + "(?, ?, ?, ?, ?, ?)", new AddBookmarkHandler(bookmark_name, char_id, x, y, z, world_id));

                PacketSendUtility.SendMessage(player, "Bookmark " + bookmark_name + " sucessfully added to your bookmark list!");

                UpdateInfo(player.GetObjectId());
            }
            catch (Exception)
            {
                PacketSendUtility.SendMessage(player, "syntax //bk <add|del|tele> <bookmark name>");
                return;
            }
        else if (paramsArr[0].Equals("del"))
        {
            try
            {
                using (MySqlConnection con = DatabaseFactory.GetConnection())
                {
                    con.Open();
                    using (MySqlCommand statement = con.CreateCommand())
                    {
                        statement.CommandText = "DELETE FROM bookmark WHERE name = ?";
                        statement.Parameters.Add(new MySqlParameter { Value = paramsArr[1].ToLower() });
                        statement.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                PacketSendUtility.SendMessage(player, "syntax //bk <add|del|tele> <bookmark name>");
                return;
            }
            finally
            {
                PacketSendUtility.SendMessage(player, "Bookmark " + bookmark_name + " sucessfully removed from your bookmark list!");
                UpdateInfo(player.GetObjectId());
            }
        }
        else if (paramsArr[0].Equals("tele"))
            try
            {
                if (paramsArr[1].Equals("") || paramsArr[1] == null)
                {
                    PacketSendUtility.SendMessage(player, "syntax //bk <add|del|tele> <bookmark name>");
                    return;
                }

                UpdateInfo(player.GetObjectId());

                bookmark_name = paramsArr[1].ToLower();
                Bookmark tele_bk = null;
                try
                {
                    tele_bk = SelectByName(bookmark_name);
                }
                finally
                {
                    if (tele_bk != null)
                    {
                        TeleportService.TeleportTo(player, tele_bk.GetWorld_id(), tele_bk.GetX(), tele_bk.GetY(), tele_bk.GetZ());
                        PacketSendUtility.SendMessage(player, "Teleported to bookmark " + tele_bk.GetName() + " location");
                    }
                }
            }
            catch (Exception)
            {
                PacketSendUtility.SendMessage(player, "syntax //bk <add|del|tele> <bookmark name>");
                return;
            }
        else if (paramsArr[0].Equals("list"))
        {
            UpdateInfo(player.GetObjectId());
            PacketSendUtility.SendMessage(player, "=====Bookmark list begin=====");
            foreach (Bookmark b in bookmarks)
            {
                string chatLink = ChatUtil.Position(b.GetName(), b.GetWorld_id(), b.GetX(), b.GetY(), b.GetZ());
                PacketSendUtility.SendMessage(player,
                    " = " + chatLink + " =  " + WorldMapTypeExtensions.GetWorld(b.GetWorld_id()) + "  ( " + b.GetX() + " ," + b.GetY() + " ," + b.GetZ() + " )");
            }
            PacketSendUtility.SendMessage(player, "=====Bookmark list end=======");
        }
    }

    /// <summary>Reload bookmark list from db</summary>
    public void UpdateInfo(int objId)
    {
        bookmarks.Clear();

        DB.Select("SELECT * FROM `bookmark` where char_id= ?", new UpdateInfoHandler(objId, bookmarks));
    }

    /// <param name="bk_name">bookmark name</param>
    /// <returns>Bookmark from bookmark name</returns>
    internal Bookmark SelectByName(string bk_name)
    {
        foreach (Bookmark b in bookmarks)
            if (b.GetName().Equals(bk_name))
                return b;
        return null;
    }

    /// <param name="bk_name">bookmark name</param>
    /// <returns>true if bookmark exists</returns>
    public bool IsBookmarkExists(string bk_name, int objId)
    {
        int bkcount = 0;
        try
        {
            using (MySqlConnection con = DatabaseFactory.GetConnection())
            {
                con.Open();
                using (MySqlCommand statement = con.CreateCommand())
                {
                    statement.CommandText = "SELECT count(id) as bkcount FROM bookmark WHERE ? = name AND char_id = ?";
                    statement.Parameters.Add(new MySqlParameter { Value = bk_name });
                    statement.Parameters.Add(new MySqlParameter { Value = objId });
                    using (MySqlDataReader rset = statement.ExecuteReader())
                    {
                        while (rset.Read())
                            bkcount = rset.GetInt32(rset.GetOrdinal("bkcount"));
                    }
                }
            }
        }
        catch (Exception e)
        {
            log.LogError(e, "Error in reading db");
        }
        return bkcount > 0;
    }

    private sealed class AddBookmarkHandler : IUStH
    {
        private readonly string bookmark_name;
        private readonly int char_id;
        private readonly float x, y, z;
        private readonly int world_id;

        public AddBookmarkHandler(string bookmark_name, int char_id, float x, float y, float z, int world_id)
        {
            this.bookmark_name = bookmark_name;
            this.char_id = char_id;
            this.x = x;
            this.y = y;
            this.z = z;
            this.world_id = world_id;
        }

        public void HandleInsertUpdate(MySqlCommand ps)
        {
            ps.Parameters.Add(new MySqlParameter { Value = bookmark_name });
            ps.Parameters.Add(new MySqlParameter { Value = char_id });
            ps.Parameters.Add(new MySqlParameter { Value = x });
            ps.Parameters.Add(new MySqlParameter { Value = y });
            ps.Parameters.Add(new MySqlParameter { Value = z });
            ps.Parameters.Add(new MySqlParameter { Value = world_id });
            ps.ExecuteNonQuery();
        }
    }

    private sealed class UpdateInfoHandler : ParamReadStH
    {
        private readonly int objId;
        private readonly List<Bookmark> bookmarks;

        public UpdateInfoHandler(int objId, List<Bookmark> bookmarks)
        {
            this.objId = objId;
            this.bookmarks = bookmarks;
        }

        public void SetParams(MySqlCommand stmt)
        {
            stmt.Parameters.Add(new MySqlParameter { Value = objId });
        }

        public void HandleRead(MySqlDataReader rset)
        {
            while (rset.Read())
            {
                string name = rset.GetString(rset.GetOrdinal("name"));
                float x = rset.GetFloat(rset.GetOrdinal("x"));
                float y = rset.GetFloat(rset.GetOrdinal("y"));
                float z = rset.GetFloat(rset.GetOrdinal("z"));
                int world_id = rset.GetInt32(rset.GetOrdinal("world_id"));
                bookmarks.Add(new Bookmark(x, y, z, world_id, name));
            }
        }
    }
}

class Bookmark
{
    private string name;
    private float x;
    private float y;
    private float z;
    private int world_id;

    public Bookmark(float x, float y, float z, int world_id, string name)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.world_id = world_id;
        this.name = name;
    }

    /// <returns>the name</returns>
    public string GetName()
    {
        return name;
    }

    /// <returns>the x</returns>
    public float GetX()
    {
        return x;
    }

    /// <returns>the y</returns>
    public float GetY()
    {
        return y;
    }

    /// <returns>the z</returns>
    public float GetZ()
    {
        return z;
    }

    /// <returns>the world_id</returns>
    public int GetWorld_id()
    {
        return world_id;
    }
}
