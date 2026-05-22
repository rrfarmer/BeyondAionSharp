using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ExpExtractServiceTests
{
	[Fact]
	public async Task Validate_ComputesPercentCostFromCurrentLevelExpNeed()
	{
		var staticData = await LoadStaticDataAsync(percent: true, cost: 10);
		var player = CreatePlayer(exp: 1500);
		var sourceTemplate = staticData.ItemTemplates.GetItemTemplate(100)!;

		var validation = ExpExtractService.Validate(player, sourceTemplate, staticData);

		Assert.True(validation.Succeeded);
		Assert.Equal(200, validation.RequiredExp);
		Assert.Equal(1300, validation.NewExp);
		Assert.Equal(200, validation.RewardTemplate?.TemplateId);
	}

	[Fact]
	public async Task Validate_RejectsWhenExtractionWouldDropBelowLevelStart()
	{
		var staticData = await LoadStaticDataAsync(percent: false, cost: 600);
		var player = CreatePlayer(exp: 1500);
		var sourceTemplate = staticData.ItemTemplates.GetItemTemplate(100)!;

		var validation = ExpExtractService.Validate(player, sourceTemplate, staticData);

		Assert.False(validation.Succeeded);
		Assert.Equal(ExpExtractFailure.NotEnoughExp, validation.Failure);
	}

	[Fact]
	public async Task CreateMutationPlan_ConsumesSourceAndAddsReward()
	{
		var staticData = await LoadStaticDataAsync(percent: false, cost: 100);
		var player = CreatePlayer(exp: 1500);
		var sourceTemplate = staticData.ItemTemplates.GetItemTemplate(100)!;
		var validation = ExpExtractService.Validate(player, sourceTemplate, staticData);

		var plan = ExpExtractService.CreateMutationPlan(
			player,
			player.InventoryItems,
			sourceTemplate,
			validation,
			staticData.ItemTemplates,
			() => 9001);

		Assert.True(plan.Succeeded);
		Assert.True(plan.RewardSucceeded);
		Assert.False(plan.RewardInventoryFull);
		Assert.Equal(1, plan.SourceItemUpdate?.ObjectId);
		Assert.Equal(1, plan.SourceItemUpdate?.Count);
		Assert.Null(plan.DeletedSourceItemObjectId);
		var reward = Assert.Single(plan.AddedRewardItems);
		Assert.Equal(9001, reward.ObjectId);
		Assert.Equal(200, reward.ItemId);
		Assert.Empty(plan.UpdatedRewardItems);
	}

	private static Player CreatePlayer(long exp)
	{
		return new Player
		{
			ObjectId = 700,
			Exp = exp,
			InventoryItems =
			[
				new InventoryItem { ObjectId = 1, ItemId = 100, Location = 0, Count = 2, OwnerId = 700 },
			],
		};
	}

	private static async Task<StaticData> LoadStaticDataAsync(bool percent, long cost)
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			$$"""
			<static_data>
				<player_experience_table>
					<exp>0</exp>
					<exp>1000</exp>
					<exp>3000</exp>
				</player_experience_table>
				<item_templates>
					<item_template id="100" name="Extractor" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="10">
						<actions>
							<expextract item_id="200" percent="{{percent.ToString().ToLowerInvariant()}}" cost="{{cost}}"/>
						</actions>
					</item_template>
					<item_template id="200" name="Reward" level="1" item_group="NONE" item_type="NORMAL" quality="COMMON" race="PC_ALL" max_stack_count="1"/>
				</item_templates>
			</static_data>
			""");
		return await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());
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
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-exp-extract-" + Guid.NewGuid().ToString("N"));
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
