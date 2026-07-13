using System.Reflection;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Dao;
using Aion.GameServer.Handlers.ConsoleCommands;

namespace Aion.GameServer.Tests;

[Xunit.Collection("GoldenDataManager")]
public sealed class GmBookmarkPortTests
{
    [Fact]
    public void AdminCommandReusesConsoleBookmarkAccessLevel()
    {
        Dictionary<string, sbyte> original = CommandsConfig.ACCESS_LEVELS;
        try
        {
            CommandsConfig.ACCESS_LEVELS = new Dictionary<string, sbyte> { [Bookmark_add.ALIAS] = 9 };
            var command = new Aion.GameServer.Handlers.AdminCommands.Bookmark();

            Assert.Equal("bookmark", command.GetAlias());
            Assert.Equal((byte)9, command.GetLevel());
            Assert.Contains("deleteAll", command.GetSyntaxInfo());
        }
        finally
        {
            CommandsConfig.ACCESS_LEVELS = original;
        }
    }

    [Fact]
    public void DaoQueriesScopeBookmarksByPlayerAndName()
    {
        Assert.Equal("SELECT * FROM `bookmark` where player_id= ?", Constant("LOAD_QUERY"));
        Assert.Equal("REPLACE INTO `bookmark` (player_id, name, world_id, x, y, z) VALUES (?, ?, ?, ?, ?, ?)", Constant("STORE_QUERY"));
        Assert.Equal("DELETE FROM `bookmark` WHERE player_id = ? and name = ?", Constant("DELETE_QUERY"));
        Assert.Equal("DELETE FROM `bookmark` WHERE player_id = ?", Constant("DELETE_ALL_QUERY"));
    }

    [Fact]
    public void DatabaseMigrationUsesPlayerScopedCompositeKey()
    {
        string schema = File.ReadAllText(RepoFile("game-server", "sql", "aion_gs.sql"));
        string update = File.ReadAllText(RepoFile("game-server", "sql", "update.sql"));

        Assert.Contains("`name` varchar(27) NOT NULL", schema);
        Assert.Contains("PRIMARY KEY (`player_id`, `name`)", schema);
        Assert.Contains("CHANGE COLUMN `char_id` `player_id` INT NOT NULL FIRST", update);
        Assert.Contains("ADD PRIMARY KEY (`player_id`, `name`)", update);
        Assert.Contains("FOREIGN KEY (`player_id`) REFERENCES `players` (`id`) ON DELETE CASCADE ON UPDATE CASCADE", update);
    }

    private static string Constant(string name)
    {
        FieldInfo field = typeof(BookmarkDAO).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(BookmarkDAO).FullName, name);
        return (string)field.GetRawConstantValue()!;
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not find repository file", Path.Combine(parts));
    }
}
