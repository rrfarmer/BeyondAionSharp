using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ApExtractServiceTests
{
	private const int ToolItemId = 165005000;
	private const int TargetSwordItemId = 100000363;
	private const int TargetArmorItemId = 110100001;
	private const int TargetAccessoryItemId = 120000001;
	private const int NoApExtractSwordItemId = 100000364;

	[Fact]
	public void CreateMutationPlan_DeletesTargetConsumesToolAndAddsAbyssPoints()
	{
		var player = CreatePlayer(
			CreateItem(1001, TargetSwordItemId),
			CreateItem(2001, ToolItemId, count: 2));

		var plan = ApExtractService.CreateMutationPlan(
			player,
			extractionToolObjectId: 2001,
			targetItemObjectId: 1001,
			CreateItemTemplates());

		Assert.True(plan.Succeeded);
		Assert.Equal(1001, plan.DeletedTargetItemObjectId);
		Assert.Equal(1, plan.SourceItemUpdate?.Count);
		Assert.Null(plan.DeletedSourceItemObjectId);
		Assert.Equal(980, plan.AbyssPoints);
		Assert.Equal(980, plan.AbyssRankUpdate?.Ap);
		Assert.DoesNotContain(plan.InventoryItems, item => item.ObjectId == 1001);
		Assert.Contains(plan.InventoryItems, item => item.ObjectId == 2001 && item.Count == 1);
	}

	[Fact]
	public void CreateMutationPlan_RequiresLevelQualityMaskAndTargetType()
	{
		var templates = CreateItemTemplates();

		Assert.Equal(
			ApExtractFailure.CannotAct,
			ApExtractService.CreateMutationPlan(
				CreatePlayer(CreateItem(1001, TargetArmorItemId), CreateItem(2001, ToolItemId)),
				2001,
				1001,
				templates).Failure);
		Assert.Equal(
			ApExtractFailure.CannotAct,
			ApExtractService.CreateMutationPlan(
				CreatePlayer(CreateItem(1001, TargetAccessoryItemId), CreateItem(2001, ToolItemId)),
				2001,
				1001,
				templates).Failure);
		Assert.Equal(
			ApExtractFailure.CannotAct,
			ApExtractService.CreateMutationPlan(
				CreatePlayer(CreateItem(1001, NoApExtractSwordItemId), CreateItem(2001, ToolItemId)),
				2001,
				1001,
				templates).Failure);
	}

	private static Player CreatePlayer(params InventoryItem[] items)
	{
		return new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default(),
			InventoryItems = items,
		};
	}

	private static InventoryItem CreateItem(int objectId, int itemId, long count = 1)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			Location = 0,
			OwnerId = 700,
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		const int canApExtractMask = 1 << 16;
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(
				ToolItemId,
				"TestAPExtractionTool_20%_40lv_common_Weapon",
				0,
				0,
				40,
				"NONE",
				"NORMAL",
				"RARE",
				"PC_ALL",
				100,
				0,
				0,
				ApExtractAction: new ItemApExtractActionInfo(0.2f, "WEAPON")),
			new ItemTemplateSummary(
				TargetSwordItemId,
				"Warrior's Sword",
				0,
				canApExtractMask,
				30,
				"SWORD",
				"ABYSS",
				"RARE",
				"PC_ALL",
				1,
				0,
				1,
				RequiredAbyssPoints: 4900),
			new ItemTemplateSummary(
				NoApExtractSwordItemId,
				"Sainted Sword",
				0,
				0,
				30,
				"SWORD",
				"ABYSS",
				"RARE",
				"PC_ALL",
				1,
				0,
				1,
				RequiredAbyssPoints: 4900),
			new ItemTemplateSummary(
				TargetArmorItemId,
				"Leather Jerkin",
				0,
				canApExtractMask,
				30,
				"LT_TORSO",
				"ABYSS",
				"RARE",
				"PC_ALL",
				1,
				0,
				1,
				RequiredAbyssPoints: 4900),
			new ItemTemplateSummary(
				TargetAccessoryItemId,
				"Abyss Ring",
				0,
				canApExtractMask,
				30,
				"RING",
				"ABYSS",
				"RARE",
				"PC_ALL",
				1,
				0,
				1,
				RequiredAbyssPoints: 4900),
		]);
	}
}
