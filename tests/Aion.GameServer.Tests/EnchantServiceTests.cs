using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class EnchantServiceTests
{
	private const int SwordItemId = 100100860;
	private const int PlainSwordItemId = 100000001;
	private const int UniversalMaterialItemId = 166500002;
	private const int InvalidMaterialItemId = 166500003;
	private const int ToolItemId = 165030001;

	[Fact]
	public void CreateAmplificationPlan_AmplifiesTargetAndConsumesSources()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, enchant: 15),
			CreateItem(2001, SwordItemId, count: 2),
			CreateItem(2002, ToolItemId));

		var plan = EnchantService.CreateAmplificationPlan(
			player,
			targetItemObjectId: 1001,
			materialObjectId: 2001,
			toolObjectId: 2002,
			CreateItemTemplates());

		Assert.True(plan.Succeeded);
		Assert.True(plan.TargetItemUpdate?.IsAmplified);
		Assert.Equal(1, plan.MaterialItemUpdate?.Count);
		Assert.Equal(2002, plan.DeletedToolItemObjectId);
		Assert.Contains(plan.InventoryItems, item => item.ObjectId == 1001 && item.IsAmplified);
		Assert.Contains(plan.InventoryItems, item => item.ObjectId == 2001 && item.Count == 1);
		Assert.DoesNotContain(plan.InventoryItems, item => item.ObjectId == 2002);
	}

	[Fact]
	public void CreateAmplificationPlan_AllowsUniversalMaterial()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, enchant: 15),
			CreateItem(2001, UniversalMaterialItemId),
			CreateItem(2002, ToolItemId));

		var plan = EnchantService.CreateAmplificationPlan(
			player,
			targetItemObjectId: 1001,
			materialObjectId: 2001,
			toolObjectId: 2002,
			CreateItemTemplates());

		Assert.True(plan.Succeeded);
		Assert.Equal(2001, plan.DeletedMaterialItemObjectId);
		Assert.Equal(2002, plan.DeletedToolItemObjectId);
	}

	[Fact]
	public void CreateAmplificationPlan_ReturnsJavaShapedFailures()
	{
		var templates = CreateItemTemplates();
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, enchant: 15),
			CreateItem(1002, SwordItemId, enchant: 15, isAmplified: true),
			CreateItem(1003, PlainSwordItemId, enchant: 15),
			CreateItem(1004, SwordItemId, enchant: 14),
			CreateItem(2001, SwordItemId),
			CreateItem(2002, ToolItemId),
			CreateItem(2003, InvalidMaterialItemId));

		Assert.Equal(
			AmplificationFailure.NoTargetItem,
			EnchantService.CreateAmplificationPlan(player, 404, 2001, 2002, templates).Failure);
		Assert.Equal(
			AmplificationFailure.NoTargetItem,
			EnchantService.CreateAmplificationPlan(player, 1001, 404, 2002, templates).Failure);
		Assert.Equal(
			AmplificationFailure.AlreadyAmplified,
			EnchantService.CreateAmplificationPlan(player, 1002, 2001, 2002, templates).Failure);
		Assert.Equal(
			AmplificationFailure.CannotAmplify,
			EnchantService.CreateAmplificationPlan(player, 1003, 2001, 2002, templates).Failure);
		Assert.Equal(
			AmplificationFailure.NeedsMaxEnchant,
			EnchantService.CreateAmplificationPlan(player, 1004, 2001, 2002, templates).Failure);
		Assert.Equal(
			AmplificationFailure.NoTargetItem,
			EnchantService.CreateAmplificationPlan(player, 1001, 2003, 2002, templates).Failure);
	}

	private static Player CreatePlayer(params InventoryItem[] items)
	{
		return new Player
		{
			ObjectId = 1,
			Race = "ELYOS",
			InventoryItems = items,
		};
	}

	private static InventoryItem CreateItem(
		int objectId,
		int itemId,
		long count = 1,
		int enchant = 0,
		bool isAmplified = false)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			Location = 0,
			Enchant = enchant,
			IsAmplified = isAmplified,
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(
				SwordItemId,
				"Tac Officer's Cudgel",
				0,
				1,
				55,
				"MACE",
				"NORMAL",
				"EPIC",
				"PC_ALL",
				1,
				0,
				1,
				MaxEnchantLevel: 15,
				CanExceedEnchant: true),
			new ItemTemplateSummary(
				PlainSwordItemId,
				"Circulus' Sword",
				0,
				1,
				1,
				"SWORD",
				"NORMAL",
				"UNIQUE",
				"PC_ALL",
				1,
				0,
				1,
				MaxEnchantLevel: 15),
			new ItemTemplateSummary(UniversalMaterialItemId, "Amplification Material", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(InvalidMaterialItemId, "Invalid Material", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(ToolItemId, "Amplification Tool", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
		]);
	}
}
