namespace Aion.GameServer.Tests;

public sealed class GameServerSchemaSanityTests
{
	[Fact]
	public void BookmarkPrimaryKey_IsSeparatedFromForeignKeyConstraint()
	{
		string sql = File.ReadAllText(FindSchemaPath()).Replace("\r\n", "\n", StringComparison.Ordinal);

		Assert.Contains(
			"PRIMARY KEY (`player_id`, `name`),\n\tCONSTRAINT `bookmark_ibfk_1`",
			sql,
			StringComparison.Ordinal);
	}

	private static string FindSchemaPath()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory != null)
		{
			string candidate = Path.Combine(directory.FullName, "game-server", "sql", "aion_gs.sql");
			if (File.Exists(candidate))
				return candidate;
			directory = directory.Parent;
		}

		throw new FileNotFoundException("Could not find game-server/sql/aion_gs.sql.");
	}
}
