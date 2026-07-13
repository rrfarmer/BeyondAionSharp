using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class WorkOrderRecipeTableTests
{
	[Fact]
	public void Load_ReadsWorkOrderRecipeIdsLikeJavaXmlQuests()
	{
		var path = Path.GetTempFileName();
		try
		{
			File.WriteAllText(
				path,
				"""
				<quest_scripts>
					<work_order id="5000" start_npc_ids="203788 830062" recipe_id="155004001" />
					<work_order id="5001" start_npc_ids="203788 830062" recipe_id="155004002" />
				</quest_scripts>
				""");

			var table = WorkOrderRecipeTable.Load(path);

			Assert.Equal(2, table.Count);
			Assert.True(table.TryGetRecipeId(5000, out var firstRecipeId));
			Assert.Equal(155004001, firstRecipeId);
			Assert.True(table.TryGetRecipeId(5001, out var secondRecipeId));
			Assert.Equal(155004002, secondRecipeId);
			Assert.False(table.TryGetRecipeId(9999, out _));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void RealDataAudit_LoadsJavaWorkOrderRecipeIds()
	{
		var repoRoot = FindRepoRoot();
		var path = Path.Combine(repoRoot, "game-server", "data", "static_data", "quest_script_data", "work_order.xml");

		var table = WorkOrderRecipeTable.Load(path);

		Assert.Equal(574, table.Count);
		Assert.True(table.TryGetRecipeId(5000, out var firstRecipeId));
		Assert.Equal(155004001, firstRecipeId);
		Assert.True(table.TryGetRecipeId(6574, out var lateRecipeId));
		Assert.Equal(155009280, lateRecipeId);
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "AionServer.slnx"))
				&& File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "quest_script_data", "work_order.xml")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Unable to locate repository root.");
	}
}
