using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcCustomDropServiceTests
{
	[Fact]
	public async Task CustomNpcDropTable_LoadsXmlDefaultsAndLookup()
	{
		using var temp = TempDirectory.Create();
		var customDropFile = Path.Combine(temp.Path, "custom_drop.xml");
		await File.WriteAllTextAsync(
			customDropFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<custom_drop>
				<npc_drop npc_id="203001">
					<drop_group name="race-items" race="ELYOS" max_items="2" level_based_chance_reduction="true">
						<drop item_id="1001" chance="50" />
						<drop item_id="1002" min_amount="2" max_amount="4" chance="75" each_member="true" />
					</drop_group>
					<drop_group>
						<drop item_id="1003" />
					</drop_group>
				</npc_drop>
			</custom_drop>
			""");

		var table = await CustomNpcDropTable.LoadFromImportedFilesAsync([customDropFile]);

		Assert.Equal(1, table.Count);
		var npcDrop = table.GetNpcDrop(203001);
		Assert.NotNull(npcDrop);
		Assert.Equal(203001, npcDrop!.NpcId);
		Assert.Equal(2, npcDrop.Groups.Count);
		var raceGroup = npcDrop.Groups[0];
		Assert.Equal("race-items", raceGroup.Name);
		Assert.Equal("ELYOS", raceGroup.Race);
		Assert.True(raceGroup.UseLevelBasedChanceReduction);
		Assert.Equal(2, raceGroup.MaxItems);
		Assert.Equal(new CustomDropSummary(1001, 1, 1, 50f, false), raceGroup.Drops[0]);
		Assert.Equal(new CustomDropSummary(1002, 2, 4, 75f, true), raceGroup.Drops[1]);
		var defaultGroup = npcDrop.Groups[1];
		Assert.Equal("PC_ALL", defaultGroup.Race);
		Assert.Equal(1, defaultGroup.MaxItems);
		Assert.Equal(new CustomDropSummary(1003, 1, 1, 100f, false), Assert.Single(defaultGroup.Drops));
		Assert.Null(table.GetNpcDrop(404));
	}

	[Fact]
	public void CreateDrops_SelectsNearestSuccessfulDropsAndCounts()
	{
		var table = new CustomNpcDropTable(
		[
			new CustomNpcDropSummary(
				203001,
				[
					new CustomDropGroupSummary(
						"weighted",
						"PC_ALL",
						UseLevelBasedChanceReduction: false,
						MaxItems: 2,
						[
							new CustomDropSummary(1001, 1, 1, 20f, false),
							new CustomDropSummary(1002, 2, 4, 80f, false),
							new CustomDropSummary(1003, 1, 1, 10f, false),
						]),
				]),
		]);
		var chanceRolls = new Queue<float>([50f, 5f]);
		var countRolls = new Queue<int>([3, 1]);
		var service = new WorldNpcCustomDropService(table, () => chanceRolls.Dequeue(), (_, _) => countRolls.Dequeue());

		var result = service.CreateDrops(
			npcObjectId: 5001,
			npcTemplateId: 203001,
			new WorldNpcDropModifiers("ELYOS"),
			startIndex: 5);

		Assert.Equal(7, result.NextIndex);
		Assert.Collection(
			result.Drops,
			drop =>
			{
				Assert.Equal(5, drop.Index);
				Assert.Equal(1002, drop.ItemId);
				Assert.Equal(3, drop.Count);
				Assert.Equal(5001, drop.NpcObjectId);
				Assert.Null(drop.PlayerObjectIds);
			},
			drop =>
			{
				Assert.Equal(6, drop.Index);
				Assert.Equal(1003, drop.ItemId);
				Assert.Equal(1, drop.Count);
			});
	}

	[Fact]
	public void CreateDrops_AppliesRaceAndLevelChanceModifiers()
	{
		var table = new CustomNpcDropTable(
		[
			new CustomNpcDropSummary(
				203001,
				[
					new CustomDropGroupSummary(
						"elyos-only",
						"ELYOS",
						UseLevelBasedChanceReduction: true,
						MaxItems: 1,
						[new CustomDropSummary(1001, 1, 1, 60f, false)]),
					new CustomDropGroupSummary(
						"asmo-only",
						"ASMODIANS",
						UseLevelBasedChanceReduction: false,
						MaxItems: 1,
						[new CustomDropSummary(1002, 1, 1, 100f, false)]),
				]),
		]);
		var service = new WorldNpcCustomDropService(table, () => 59f);

		var result = service.CreateDrops(
			npcObjectId: 5001,
			npcTemplateId: 203001,
			new WorldNpcDropModifiers("ELYOS", BoostDropRate: 2f, ReductionDropRate: 0.5f));

		var drop = Assert.Single(result.Drops);
		Assert.Equal(1001, drop.ItemId);
	}

	[Fact]
	public void CreateDrops_CreatesDistributedDropForEachMember()
	{
		var table = new CustomNpcDropTable(
		[
			new CustomNpcDropSummary(
				203001,
				[
					new CustomDropGroupSummary(
						"member-drop",
						"PC_ALL",
						UseLevelBasedChanceReduction: false,
						MaxItems: 1,
						[new CustomDropSummary(1001, 1, 1, 100f, true)]),
				]),
		]);
		var service = new WorldNpcCustomDropService(table, () => 0f);
		var members = new[]
		{
			new Player { ObjectId = 1001 },
			new Player { ObjectId = 1002 },
		};

		var result = service.CreateDrops(5001, 203001, new WorldNpcDropModifiers("ELYOS"), members);

		Assert.Equal(3, result.NextIndex);
		Assert.Collection(
			result.Drops,
			drop =>
			{
				Assert.Equal(1, drop.Index);
				Assert.Equal([1001], drop.PlayerObjectIds);
				Assert.True(drop.IsDistributeItem);
			},
			drop =>
			{
				Assert.Equal(2, drop.Index);
				Assert.Equal([1002], drop.PlayerObjectIds);
				Assert.True(drop.IsDistributeItem);
			});
	}

	private sealed class TempDirectory : IDisposable
	{
		private TempDirectory(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public static TempDirectory Create()
		{
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-custom-drop-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return new TempDirectory(path);
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(Path, recursive: true);
			}
			catch
			{
			}
		}
	}
}
