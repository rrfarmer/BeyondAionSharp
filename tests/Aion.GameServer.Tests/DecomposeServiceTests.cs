using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class DecomposeServiceTests
{
	[Fact]
	public void CreateSelectableRewardPlan_FiltersUnavailableRewardsAndRollsCount()
	{
		var table = new DecomposableItemTable(
		[
			new DecomposableItemSummary(
				100,
				IsSelectable: true,
				[
					new ExtractedItemsCollectionSummary(
						100,
						0,
						99,
						[
							new ResultedItemSummary(200, 1, 1, "ASMODIANS", new HashSet<string>()),
							new ResultedItemSummary(201, 2, 5, "ELYOS", new HashSet<string>(["RANGER"])),
						],
						Array.Empty<RandomItemSummary>()),
				]),
		]);
		var player = new Player { Race = "ELYOS", PlayerClass = "RANGER" };

		var selectable = DecomposeService.GetSelectableItems(player, table, 100);
		var plan = DecomposeService.CreateSelectableRewardPlan(player, table, 100, index: 0, (_, max) => max);

		var reward = Assert.Single(selectable!);
		Assert.Equal(201, reward.ItemId);
		var plannedReward = Assert.Single(plan.Rewards);
		Assert.Equal(201, plannedReward.ItemId);
		Assert.Equal(5, plannedReward.Count);
	}

	[Fact]
	public async Task CreateNormalRewardPlan_SelectsLevelSuitableFixedRewards()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<static_data>
				<player_experience_table>
					<exp>0</exp>
					<exp>100</exp>
					<exp>300</exp>
				</player_experience_table>
				<item_templates>
					<item_template id="100" name="Box" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL">
						<actions>
							<decompose/>
						</actions>
					</item_template>
					<item_template id="200" name="Reward" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="100"/>
				</item_templates>
				<decomposable_items>
					<decomposable item_id="100">
						<items chance="100" minlevel="1" maxlevel="2">
							<item id="200" min_count="2" max_count="4"/>
						</items>
					</decomposable>
				</decomposable_items>
			</static_data>
			""");
		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());
		var player = new Player
		{
			Race = "ELYOS",
			PlayerClass = "RANGER",
			InventoryItems = [new InventoryItem { ObjectId = 1, ItemId = 100, Location = 0, Count = 1 }],
		};
		var sourceItem = Assert.Single(player.InventoryItems);
		var sourceTemplate = staticData.ItemTemplates.GetItemTemplate(100)!;

		var plan = DecomposeService.CreateNormalRewardPlan(player, sourceItem, sourceTemplate, staticData, rollInclusive: (_, max) => max);

		Assert.True(plan.Succeeded);
		var reward = Assert.Single(plan.Rewards);
		Assert.Equal(200, reward.ItemId);
		Assert.Equal(4, reward.Count);
	}

	[Fact]
	public async Task CanAct_RejectsSpecialCubeRewardsWhenSpecialCubeIsFull()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<static_data>
				<player_experience_table>
					<exp>0</exp>
					<exp>100</exp>
				</player_experience_table>
				<item_templates>
					<item_template id="100" name="Box" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL">
						<actions>
							<decompose/>
						</actions>
					</item_template>
					<item_template id="200" name="Special Reward" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL">
						<inventory id="2"/>
					</item_template>
					<item_template id="300" name="Special Filler" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL">
						<inventory id="2"/>
					</item_template>
				</item_templates>
				<decomposable_items>
					<decomposable item_id="100">
						<items chance="100" minlevel="1" maxlevel="1">
							<item id="200" min_count="1" max_count="1"/>
						</items>
					</decomposable>
				</decomposable_items>
			</static_data>
			""");
		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());
		var sourceItem = new InventoryItem { ObjectId = 1, ItemId = 100, Location = 0, Count = 1 };
		var player = new Player
		{
			Race = "ELYOS",
			PlayerClass = "RANGER",
			InventoryItems = Enumerable.Range(0, 102)
				.Select(index => new InventoryItem { ObjectId = 1000 + index, ItemId = 300, Location = 0, Count = 1 })
				.Prepend(sourceItem)
				.ToArray(),
		};

		var result = DecomposeService.CanAct(player, sourceItem, staticData);

		Assert.False(result.Succeeded);
		Assert.Equal(DecomposeFailure.InventoryFull, result.Failure);
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
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-decompose-" + Guid.NewGuid().ToString("N"));
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
