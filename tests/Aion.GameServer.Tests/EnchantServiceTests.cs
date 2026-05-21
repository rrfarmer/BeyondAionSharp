using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class EnchantServiceTests
{
	private const int SwordItemId = 100100860;
	private const int PlainSwordItemId = 100000001;
	private const int NoEnchantSwordItemId = 100000002;
	private const int DestructibleSwordItemId = 100000003;
	private const int UniversalMaterialItemId = 166500002;
	private const int InvalidMaterialItemId = 166500003;
	private const int ToolItemId = 165030001;
	private const int ManastoneItemId = 167000001;
	private const int HighLevelManastoneItemId = 167000002;
	private const int AlphaEnchantStoneItemId = 166000191;
	private const int DeltaEnchantStoneItemId = 166000194;
	private const int OmegaEnchantStoneItemId = 166020000;
	private const int SupplementItemId = 166100000;
	private const int AssuredSupplementItemId = 166150017;
	private const int WrongLevelSupplementItemId = 166150099;
	private const int ManastoneOnlySupplementItemId = 166150018;

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

	[Fact]
	public void CreateSocketManastonePlan_AddsStoneAndConsumesSourceOnSuccess()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId),
			CreateItem(2001, ManastoneItemId, count: 2));

		var plan = EnchantService.CreateSocketManastonePlan(
			player,
			targetItemObjectId: 1001,
			manastoneObjectId: 2001,
			targetFusedSlot: 1,
			CreateItemTemplates(),
			manastoneChances: [75f, 75f],
			rollPercent: () => 0);

		Assert.True(plan.Succeeded);
		Assert.True(plan.SocketSucceeded);
		Assert.Equal(ManastoneItemId, plan.AddedStone?.ItemId);
		Assert.Equal(1, plan.AddedStone?.Slot);
		Assert.Equal(0, plan.AddedCategory);
		Assert.Equal(1, plan.SourceItemUpdate?.Count);
		Assert.NotNull(plan.TargetItemUpdate);
		Assert.Equal([1], plan.TargetItemUpdate.ManaStones.Select(stone => stone.Slot).ToArray());
		Assert.Contains(plan.InventoryItems, item => item.ObjectId == 2001 && item.Count == 1);
	}

	[Fact]
	public void CreateSocketManastonePlan_FailureStillConsumesSourceLikeJavaAct()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId),
			CreateItem(2001, ManastoneItemId));

		var plan = EnchantService.CreateSocketManastonePlan(
			player,
			targetItemObjectId: 1001,
			manastoneObjectId: 2001,
			targetFusedSlot: 1,
			CreateItemTemplates(),
			manastoneChances: [75f, 75f],
			rollPercent: () => 99);

		Assert.True(plan.Succeeded);
		Assert.False(plan.SocketSucceeded);
		Assert.Null(plan.AddedStone);
		Assert.Equal(2001, plan.DeletedSourceItemObjectId);
		Assert.Empty(plan.TargetItemUpdate?.ManaStones ?? Array.Empty<ItemStoneSocket>());
		Assert.DoesNotContain(plan.InventoryItems, item => item.ObjectId == 2001);
		Assert.Equal(1, plan.TargetItemUpdate?.TuneCount);
	}

	[Fact]
	public void CreateSocketManastonePlan_LevelRejectionUsesFailureSideEffects()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId),
			CreateItem(2001, HighLevelManastoneItemId));

		var plan = EnchantService.CreateSocketManastonePlan(
			player,
			targetItemObjectId: 1001,
			manastoneObjectId: 2001,
			targetFusedSlot: 1,
			CreateItemTemplates(),
			manastoneChances: [75f, 75f],
			rollPercent: () => 0);

		Assert.True(plan.Succeeded);
		Assert.False(plan.SocketSucceeded);
		Assert.Equal(2001, plan.DeletedSourceItemObjectId);
		Assert.Empty(plan.TargetItemUpdate?.ManaStones ?? Array.Empty<ItemStoneSocket>());
		Assert.Equal(1, plan.TargetItemUpdate?.TuneCount);
	}

	[Fact]
	public void CreateSocketManastonePlan_ConsumesSupplementsByExistingStoneCount()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, manaStones: [new ItemStoneSocket(ManastoneItemId, 1)]),
			CreateItem(2001, ManastoneItemId),
			CreateItem(3001, SupplementItemId, count: 3));

		var plan = EnchantService.CreateSocketManastonePlan(
			player,
			targetItemObjectId: 1001,
			manastoneObjectId: 2001,
			targetFusedSlot: 1,
			CreateItemTemplates(),
			supplementObjectId: 3001,
			manastoneChances: [0f, 0f],
			rollPercent: () => 99);

		Assert.True(plan.Succeeded);
		Assert.True(plan.SocketSucceeded);
		Assert.Equal(2001, plan.DeletedSourceItemObjectId);
		var supplementUpdate = Assert.Single(plan.SupplementItemUpdates);
		Assert.Equal(3001, supplementUpdate.ObjectId);
		Assert.Equal(1, supplementUpdate.Count);
		Assert.Empty(plan.DeletedSupplementItemObjectIds);
		Assert.NotNull(plan.TargetItemUpdate);
		Assert.Equal([1, 2], plan.TargetItemUpdate.ManaStones.Select(stone => stone.Slot).ToArray());
		Assert.Contains(plan.InventoryItems, item => item.ObjectId == 3001 && item.Count == 1);
	}

	[Fact]
	public void CreateSocketManastonePlan_InsufficientSupplementsFailWithoutSupplementMutation()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, manaStones: [new ItemStoneSocket(ManastoneItemId, 1)]),
			CreateItem(2001, ManastoneItemId),
			CreateItem(3001, SupplementItemId));

		var plan = EnchantService.CreateSocketManastonePlan(
			player,
			targetItemObjectId: 1001,
			manastoneObjectId: 2001,
			targetFusedSlot: 1,
			CreateItemTemplates(),
			supplementObjectId: 3001,
			manastoneChances: [100f, 100f],
			rollPercent: () => 0);

		Assert.True(plan.Succeeded);
		Assert.False(plan.SocketSucceeded);
		Assert.Equal(2001, plan.DeletedSourceItemObjectId);
		Assert.Empty(plan.SupplementItemUpdates);
		Assert.Empty(plan.DeletedSupplementItemObjectIds);
		Assert.Contains(plan.InventoryItems, item => item.ObjectId == 3001 && item.Count == 1);
		Assert.NotNull(plan.TargetItemUpdate);
		Assert.Equal([1], plan.TargetItemUpdate.ManaStones.Select(stone => stone.Slot).ToArray());
	}

	[Fact]
	public void CreateSocketManastonePlan_ManastoneOnlySupplementConsumesSingleItem()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, manaStones: [new ItemStoneSocket(ManastoneItemId, 1)]),
			CreateItem(2001, ManastoneItemId),
			CreateItem(3001, AssuredSupplementItemId));

		var plan = EnchantService.CreateSocketManastonePlan(
			player,
			targetItemObjectId: 1001,
			manastoneObjectId: 2001,
			targetFusedSlot: 1,
			CreateItemTemplates(),
			supplementObjectId: 3001,
			manastoneChances: [0f, 0f],
			rollPercent: () => 99);

		Assert.True(plan.Succeeded);
		Assert.True(plan.SocketSucceeded);
		Assert.Empty(plan.SupplementItemUpdates);
		Assert.Equal([3001], plan.DeletedSupplementItemObjectIds);
		Assert.DoesNotContain(plan.InventoryItems, item => item.ObjectId == 3001);
	}

	[Fact]
	public void CreateSocketManastonePlan_RejectsWrongSupplementLevelBeforeAction()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId),
			CreateItem(2001, ManastoneItemId),
			CreateItem(3001, WrongLevelSupplementItemId));

		var plan = EnchantService.CreateSocketManastonePlan(
			player,
			targetItemObjectId: 1001,
			manastoneObjectId: 2001,
			targetFusedSlot: 1,
			CreateItemTemplates(),
			supplementObjectId: 3001);

		Assert.Equal(ManastoneSocketFailure.WrongSupplementLevel, plan.Failure);
		Assert.Empty(plan.InventoryItems);
		Assert.Empty(plan.SupplementItemUpdates);
		Assert.Empty(plan.DeletedSupplementItemObjectIds);
	}

	[Fact]
	public void CreateSocketManastonePlan_ReturnsPreActionFailuresWithoutMutation()
	{
		var templates = CreateItemTemplates();
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId),
			CreateItem(2001, ManastoneItemId),
			CreateItem(3001, UniversalMaterialItemId));

		Assert.Equal(
			ManastoneSocketFailure.NoSourceItem,
			EnchantService.CreateSocketManastonePlan(player, 1001, 404, 1, templates).Failure);
		Assert.Equal(
			ManastoneSocketFailure.NoTargetItem,
			EnchantService.CreateSocketManastonePlan(player, 404, 2001, 1, templates).Failure);
		Assert.Equal(
			ManastoneSocketFailure.CannotAct,
			EnchantService.CreateSocketManastonePlan(player, 1001, 3001, 1, templates).Failure);
	}

	[Fact]
	public void CreateEnchantItemPlan_IncreasesEnchantAndConsumesSourceOnSuccess()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, enchant: 14),
			CreateItem(2001, DeltaEnchantStoneItemId, count: 2));

		var plan = EnchantService.CreateEnchantItemPlan(
			player,
			targetItemObjectId: 1001,
			enchantmentStoneObjectId: 2001,
			CreateItemTemplates(),
			enchantmentStoneBaseChances: [100f, 100f],
			rollPercent: () => 0,
			criticalRollPercent: () => 99);

		Assert.True(plan.Succeeded);
		Assert.True(plan.EnchantSucceeded);
		Assert.Equal(15, plan.TargetItemUpdate?.Enchant);
		Assert.Equal(1, plan.SourceItemUpdate?.Count);
		Assert.Equal(15, plan.NewEnchantLevel);
		Assert.Contains(plan.InventoryItems, item => item.ObjectId == 1001 && item.Enchant == 15);
	}

	[Fact]
	public void CreateEnchantItemPlan_UsesSupplementCountAndCapsChance()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, enchant: 10),
			CreateItem(2001, AlphaEnchantStoneItemId),
			CreateItem(3001, SupplementItemId, count: 6));

		var plan = EnchantService.CreateEnchantItemPlan(
			player,
			targetItemObjectId: 1001,
			enchantmentStoneObjectId: 2001,
			CreateItemTemplates(),
			supplementObjectId: 3001,
			enchantmentStoneBaseChances: [100f, 100f],
			rollPercent: () => 94,
			criticalRollPercent: () => 99);

		Assert.True(plan.Succeeded);
		Assert.True(plan.EnchantSucceeded);
		Assert.Equal(11, plan.TargetItemUpdate?.Enchant);
		Assert.Equal(2001, plan.DeletedSourceItemObjectId);
		var supplementUpdate = Assert.Single(plan.SupplementItemUpdates);
		Assert.Equal(3001, supplementUpdate.ObjectId);
		Assert.Equal(2, supplementUpdate.Count);
	}

	[Fact]
	public void CreateEnchantItemPlan_FailureDowngradesAndConsumesTuneCount()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, enchant: 12),
			CreateItem(2001, AlphaEnchantStoneItemId));

		var plan = EnchantService.CreateEnchantItemPlan(
			player,
			targetItemObjectId: 1001,
			enchantmentStoneObjectId: 2001,
			CreateItemTemplates(),
			enchantmentStoneBaseChances: [0f, 0f],
			rollPercent: () => 99);

		Assert.True(plan.Succeeded);
		Assert.False(plan.EnchantSucceeded);
		Assert.Equal(10, plan.TargetItemUpdate?.Enchant);
		Assert.Equal(1, plan.TargetItemUpdate?.TuneCount);
		Assert.Equal(2001, plan.DeletedSourceItemObjectId);
	}

	[Fact]
	public void CreateEnchantItemPlan_AmplifiedFailureResetsToMaxEnchant()
	{
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, enchant: 18, isAmplified: true),
			CreateItem(2001, OmegaEnchantStoneItemId));

		var plan = EnchantService.CreateEnchantItemPlan(
			player,
			targetItemObjectId: 1001,
			enchantmentStoneObjectId: 2001,
			CreateItemTemplates(),
			enchantmentStoneAmplifiedChances: [0f, 0f],
			rollPercent: () => 99);

		Assert.True(plan.Succeeded);
		Assert.False(plan.EnchantSucceeded);
		Assert.Equal(15, plan.TargetItemUpdate?.Enchant);
		Assert.False(plan.TargetItemUpdate?.IsAmplified);
	}

	[Fact]
	public void CreateEnchantItemPlan_EnchantTypeFailureDestroysTarget()
	{
		var player = CreatePlayer(
			CreateItem(1001, DestructibleSwordItemId, enchant: 4),
			CreateItem(2001, AlphaEnchantStoneItemId));

		var plan = EnchantService.CreateEnchantItemPlan(
			player,
			targetItemObjectId: 1001,
			enchantmentStoneObjectId: 2001,
			CreateItemTemplates(),
			enchantmentStoneBaseChances: [0f, 0f],
			rollPercent: () => 99);

		Assert.True(plan.Succeeded);
		Assert.False(plan.EnchantSucceeded);
		Assert.True(plan.TargetDestroyed);
		Assert.Equal(1001, plan.DeletedTargetItemObjectId);
		Assert.DoesNotContain(plan.InventoryItems, item => item.ObjectId == 1001);
	}

	[Fact]
	public void CreateEnchantItemPlan_ReturnsJavaShapedGuardFailures()
	{
		var templates = CreateItemTemplates();
		var player = CreatePlayer(
			CreateItem(1001, SwordItemId, enchant: 15),
			CreateItem(1002, PlainSwordItemId),
			CreateItem(1003, NoEnchantSwordItemId),
			CreateItem(1004, SwordItemId, enchant: 15, isAmplified: true),
			CreateItem(1005, SwordItemId),
			CreateItem(2001, AlphaEnchantStoneItemId),
			CreateItem(2002, OmegaEnchantStoneItemId),
			CreateItem(3001, WrongLevelSupplementItemId),
			CreateItem(3002, ManastoneOnlySupplementItemId));

		Assert.Equal(
			EnchantItemFailure.NoTargetItem,
			EnchantService.CreateEnchantItemPlan(player, 404, 2001, templates).Failure);
		Assert.Equal(
			EnchantItemFailure.CannotEnchantMoreTime,
			EnchantService.CreateEnchantItemPlan(player, 1001, 2001, templates).Failure);
		Assert.Equal(
			EnchantItemFailure.CannotEnchant,
			EnchantService.CreateEnchantItemPlan(player, 1002, 2001, templates).Failure);
		Assert.Equal(
			EnchantItemFailure.CannotAct,
			EnchantService.CreateEnchantItemPlan(player, 1003, 2001, templates).Failure);
		Assert.Equal(
			EnchantItemFailure.AmplifiedNeedsOmega,
			EnchantService.CreateEnchantItemPlan(player, 1004, 2001, templates).Failure);
		Assert.Equal(
			EnchantItemFailure.WrongSupplementLevel,
			EnchantService.CreateEnchantItemPlan(player, 1005, 2002, templates, supplementObjectId: 3001).Failure);

		var manastoneOnlySupplementPlan = EnchantService.CreateEnchantItemPlan(
			player,
			targetItemObjectId: 1005,
			enchantmentStoneObjectId: 2002,
			templates,
			supplementObjectId: 3002,
			enchantmentStoneBaseChances: [100f, 100f],
			rollPercent: () => 0);

		Assert.True(manastoneOnlySupplementPlan.Succeeded);
		Assert.False(manastoneOnlySupplementPlan.EnchantSucceeded);
		Assert.Empty(manastoneOnlySupplementPlan.SupplementItemUpdates);
		Assert.Empty(manastoneOnlySupplementPlan.DeletedSupplementItemObjectIds);
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
		bool isAmplified = false,
		IReadOnlyList<ItemStoneSocket>? manaStones = null)
	{
		var item = new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			Location = 0,
			Enchant = enchant,
			IsAmplified = isAmplified,
		};
		item.ManaStones = manaStones ?? Array.Empty<ItemStoneSocket>();
		return item;
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
				CanExceedEnchant: true,
				MaxTuneCount: 1,
				ManastoneSlots: 4,
				SpecialManastoneSlots: 1),
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
				1),
			new ItemTemplateSummary(
				NoEnchantSwordItemId,
				"No-Enchant Sword",
				0,
				1 << 9,
				1,
				"SWORD",
				"NORMAL",
				"UNIQUE",
				"PC_ALL",
				1,
				0,
				1,
				MaxEnchantLevel: 15),
			new ItemTemplateSummary(
				DestructibleSwordItemId,
				"Destructible Sword",
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
				EnchantType: 1,
				MaxEnchantLevel: 15),
			new ItemTemplateSummary(UniversalMaterialItemId, "Amplification Material", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(InvalidMaterialItemId, "Invalid Material", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(ToolItemId, "Amplification Tool", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
			new ItemTemplateSummary(AlphaEnchantStoneItemId, "Alpha Enchantment Stone", 0, 0, 20, "ENCHANTMENT", "NORMAL", "RARE", "PC_ALL", 1, 0, 0, EnchantAction: new ItemEnchantActionInfo(2, 0, 0, false, 0)),
			new ItemTemplateSummary(DeltaEnchantStoneItemId, "Delta Enchantment Stone", 0, 0, 60, "ENCHANTMENT", "NORMAL", "EPIC", "PC_ALL", 1, 0, 0, EnchantAction: new ItemEnchantActionInfo(2, 0, 0, false, 0)),
			new ItemTemplateSummary(OmegaEnchantStoneItemId, "Omega Enchantment Stone", 0, 0, 65, "ENCHANTMENT", "NORMAL", "MYTHIC", "PC_ALL", 1, 0, 0, EnchantAction: new ItemEnchantActionInfo(2, 0, 0, false, 0)),
			new ItemTemplateSummary(ManastoneItemId, "Manastone: HP +20", 0, 0, 50, "MANASTONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0, EnchantAction: new ItemEnchantActionInfo(1, 0, 0, false, 0)),
			new ItemTemplateSummary(HighLevelManastoneItemId, "Manastone: HP +95", 0, 0, 80, "MANASTONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0, EnchantAction: new ItemEnchantActionInfo(1, 0, 0, false, 0)),
			new ItemTemplateSummary(SupplementItemId, "Lesser Supplements (Heroic or Less)", 0, 0, 30, "NONE", "NORMAL", "LEGEND", "PC_ALL", 1, 0, 0, EnchantAction: new ItemEnchantActionInfo(0, 0, 0, false, 100)),
			new ItemTemplateSummary(AssuredSupplementItemId, "Assured Greater Felicitous Socketing (Fabled)", 0, 0, 65, "NONE", "NORMAL", "UNIQUE", "PC_ALL", 1, 0, 0, EnchantAction: new ItemEnchantActionInfo(0, 1, 65, true, 100)),
			new ItemTemplateSummary(WrongLevelSupplementItemId, "Wrong Level Supplement", 0, 0, 10, "NONE", "NORMAL", "UNIQUE", "PC_ALL", 1, 0, 0, EnchantAction: new ItemEnchantActionInfo(0, 1, 10, true, 100)),
			new ItemTemplateSummary(ManastoneOnlySupplementItemId, "Manastone-Only Supplement", 0, 0, 65, "NONE", "NORMAL", "UNIQUE", "PC_ALL", 1, 0, 0, EnchantAction: new ItemEnchantActionInfo(0, 1, 65, true, 100)),
		]);
	}
}
