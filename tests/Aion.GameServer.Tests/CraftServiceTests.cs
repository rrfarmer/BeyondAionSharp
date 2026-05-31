using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Aion.Commons.Network;

namespace Aion.GameServer.Tests;

public sealed class CraftServiceTests
{
	[Fact]
	public async Task SpendRecipeDpForCraftStartAsync_SpendsRecipeDpAfterCraftValidation()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1100, dp: 1200);
		var recipe = CreateRecipe(recipeId: 155000001, dp: 600);

		var result = await service.SpendRecipeDpForCraftStartAsync(player, recipe, maxDp: 4000);

		Assert.Equal(CraftStartDpCostStatus.Applied, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(recipe.RecipeId, result.RecipeId);
		Assert.Equal(600, result.RequiredDp);
		Assert.Equal(1200, result.PreviousDp);
		Assert.Equal(600, result.CurrentDp);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, result.Change.Status);
		Assert.Equal(600, result.Change.AppliedValue);
		Assert.Equal(600, player.Dp);
		Assert.NotNull(result.Change.DpInfoPacket);
		Assert.NotNull(result.Change.DpStatUpdatePacket);
		AssertVisualStatsUpdate(result.Change);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Same(result.Change.DpInfoPacket, registry.Broadcasts[0].Packet);
		Assert.Same(result.Change.VisualStatsUpdate!.SpeedPacket, registry.Broadcasts[1].Packet);
		Assert.Collection(
			registry.SentPackets,
			delivery => Assert.Same(result.Change.VisualStatsUpdate!.StatsPacket, delivery.Packet),
			delivery => Assert.Same(result.Change.DpStatUpdatePacket, delivery.Packet));
		Assert.Collection(
			registry.PacketOrder,
			packet => Assert.Same(result.Change.DpInfoPacket, packet),
			packet => Assert.Same(result.Change.VisualStatsUpdate!.StatsPacket, packet),
			packet => Assert.Same(result.Change.VisualStatsUpdate!.SpeedPacket, packet),
			packet => Assert.Same(result.Change.DpStatUpdatePacket, packet));
	}

	[Fact]
	public async Task SpendRecipeDpForCraftStartAsync_RejectsInsufficientDpBeforeMutation()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1101, dp: 300);
		var recipe = CreateRecipe(recipeId: 155000002, dp: 600);

		var result = await service.SpendRecipeDpForCraftStartAsync(player, recipe, maxDp: 4000);

		Assert.Equal(CraftStartDpCostStatus.NotEnoughDp, result.Status);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(recipe.RecipeId, result.RecipeId);
		Assert.Equal(600, result.RequiredDp);
		Assert.Equal(300, result.CurrentDp);
		Assert.Equal(300, player.Dp);
		Assert.Null(result.Change);
		Assert.Empty(registry.Broadcasts);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task SpendRecipeDpForCraftStartAsync_RoutesZeroCostThroughDpBoundary()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1102, dp: 300);
		var recipe = CreateRecipe(recipeId: 155000003, dp: 0);

		var result = await service.SpendRecipeDpForCraftStartAsync(player, recipe, maxDp: 4000);

		Assert.Equal(CraftStartDpCostStatus.Applied, result.Status);
		Assert.Equal(0, result.RequiredDp);
		Assert.Equal(300, result.PreviousDp);
		Assert.Equal(300, result.CurrentDp);
		Assert.NotNull(result.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.NoChange, result.Change.Status);
		Assert.Equal(0, result.Change.AppliedValue);
		Assert.Equal(300, player.Dp);
		Assert.NotNull(result.Change.DpInfoPacket);
		Assert.NotNull(result.Change.DpStatUpdatePacket);
		AssertVisualStatsUpdate(result.Change);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Same(result.Change.DpInfoPacket, registry.Broadcasts[0].Packet);
		Assert.Same(result.Change.VisualStatsUpdate!.SpeedPacket, registry.Broadcasts[1].Packet);
		Assert.Collection(
			registry.SentPackets,
			delivery => Assert.Same(result.Change.VisualStatsUpdate!.StatsPacket, delivery.Packet),
			delivery => Assert.Same(result.Change.DpStatUpdatePacket, delivery.Packet));
	}

	[Fact]
	public async Task SpendRecipeDpForCraftStartAsync_RequiresPlayerRecipeAndUsesOnlineMaxDp()
	{
		var service = CreateService(out var registry);
		var player = CreatePlayer(objectId: 1103, dp: 600);
		var recipe = CreateRecipe(recipeId: 155000004, dp: 100);

		var missingPlayer = await service.SpendRecipeDpForCraftStartAsync(player: null, recipe, maxDp: 4000);
		var missingRecipe = await service.SpendRecipeDpForCraftStartAsync(player, recipeTemplate: null, maxDp: 4000);
		var liveMax = await service.SpendRecipeDpForCraftStartAsync(player, recipe);

		Assert.Equal(CraftStartDpCostStatus.MissingPlayer, missingPlayer.Status);
		Assert.Equal(CraftStartDpCostStatus.MissingRecipe, missingRecipe.Status);
		Assert.Equal(CraftStartDpCostStatus.Applied, liveMax.Status);
		Assert.NotNull(liveMax.Change);
		Assert.Equal(WorldNpcResourceChangeStatus.Reduced, liveMax.Change.Status);
		Assert.Equal(4000, liveMax.Change.MaxValue);
		Assert.Equal(500, player.Dp);
		Assert.Equal(2, registry.Broadcasts.Count);
		Assert.Equal(2, registry.SentPackets.Count);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_ReportsMissingRecipeOrProductTemplate()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1104, dp: 600);
		var recipe = CreateRecipe(recipeId: 155000005, dp: 0, productId: 199999999);

		var missingRecipe = service.CreateStartCraftingValidationPlan(
			player,
			recipeTemplate: null,
			productTemplate: null,
			target: null,
			targetIsStaticObject: false,
			targetIsWithinToolRange: false,
			hasCraftingTaskInProgress: false);
		var missingProduct = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate: null,
			target: null,
			targetIsStaticObject: false,
			targetIsWithinToolRange: false,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.MissingRecipe, missingRecipe.Status);
		Assert.True(missingRecipe.ShouldSendCancelCraft);
		Assert.False(missingRecipe.IsReadyForNextValidation);
		Assert.Equal(CraftStartValidationStatus.MissingProductTemplate, missingProduct.Status);
		Assert.Equal(recipe.RecipeId, missingProduct.RecipeId);
		Assert.Equal(recipe.ProductId, missingProduct.ProductItemId);
		Assert.True(missingProduct.ShouldSendCancelCraft);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_RejectsInProgressBeforeTargetValidation()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1105, dp: 600);
		var recipe = CreateRecipe(recipeId: 155000006, dp: 0, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);

		var plan = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target: null,
			targetIsStaticObject: false,
			targetIsWithinToolRange: false,
			hasCraftingTaskInProgress: true);

		Assert.Equal(CraftStartValidationStatus.AlreadyCrafting, plan.Status);
		Assert.True(plan.ShouldSendCancelCraft);
		Assert.Contains("CraftingTask", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_AllowsMorphRecipeWithoutStaticTarget()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1106, dp: 600);
		var recipe = CreateRecipe(recipeId: 155000007, dp: 0, productId: 152000401, skillId: CraftStartValidationPlan.MorphSubstancesSkillId);
		var productTemplate = CreateItemTemplates().GetItemTemplate(152000401);

		var plan = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target: null,
			targetIsStaticObject: false,
			targetIsWithinToolRange: false,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.ReadyForNextValidation, plan.Status);
		Assert.True(plan.IsMorphRecipe);
		Assert.True(plan.IsReadyForNextValidation);
		Assert.False(plan.ShouldSendCancelCraft);
		Assert.Contains("morphing", plan.JavaSource, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_RejectsNonMorphMissingOrNonStaticTarget()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1107, dp: 600);
		var recipe = CreateRecipe(recipeId: 155000008, dp: 0, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var nonStaticTarget = CreateTarget(objectId: 9001, templateId: 730190);

		var missingTarget = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target: null,
			targetIsStaticObject: false,
			targetIsWithinToolRange: false,
			hasCraftingTaskInProgress: false);
		var nonStatic = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			nonStaticTarget,
			targetIsStaticObject: false,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.InvalidNonMorphTarget, missingTarget.Status);
		Assert.Equal(CraftStartValidationStatus.InvalidNonMorphTarget, nonStatic.Status);
		Assert.True(nonStatic.ShouldSendCancelCraft);
		Assert.Contains("StaticObject", nonStatic.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_RejectsNonMorphTargetTooFar()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1108, dp: 600);
		var recipe = CreateRecipe(recipeId: 155000009, dp: 0, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9002, templateId: 730190);

		var plan = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: false,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.TooFarFromTool, plan.Status);
		Assert.Equal(730190, plan.TargetTemplateId);
		Assert.True(plan.ShouldSendCancelCraft);
		Assert.Contains("PositionUtil", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_NonMorphStaticTargetContinuesToLaterGuards()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1109, dp: 600);
		var recipe = CreateRecipe(recipeId: 155000010, dp: 0, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9003, templateId: 730190);

		var plan = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.ReadyForNextValidation, plan.Status);
		Assert.Equal(9003, plan.TargetObjectId);
		Assert.False(plan.IsMorphRecipe);
		Assert.True(plan.IsReadyForNextValidation);
		Assert.False(plan.ShouldSendCancelCraft);
		Assert.Contains("continue", plan.JavaSource, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_ChecksDpAfterTargetValidation()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1110, dp: 100);
		var recipe = CreateRecipe(recipeId: 155000011, dp: 600, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9004, templateId: 730190);

		var invalidTarget = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: false,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		var notEnoughDp = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.InvalidNonMorphTarget, invalidTarget.Status);
		Assert.Equal(CraftStartValidationStatus.NotEnoughDp, notEnoughDp.Status);
		Assert.Equal(600, notEnoughDp.RequiredDp);
		Assert.Equal(100, notEnoughDp.CurrentDp);
		Assert.Equal(9004, notEnoughDp.TargetObjectId);
		Assert.True(notEnoughDp.ShouldSendCancelCraft);
		Assert.False(notEnoughDp.IsReadyForNextValidation);
		Assert.Contains("getDp", notEnoughDp.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_AllowsSufficientDpToContinue()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1111, dp: 700);
		var recipe = CreateRecipe(recipeId: 155000012, dp: 600, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9005, templateId: 730190);

		var plan = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.ReadyForNextValidation, plan.Status);
		Assert.Equal(600, plan.RequiredDp);
		Assert.Equal(700, plan.CurrentDp);
		Assert.False(plan.ShouldSendCancelCraft);
		Assert.True(plan.IsReadyForNextValidation);
	}

	[Fact]
	public void CreateStartCancelPacketPlan_PlansJavaCancelUpdateAndAnimation()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1112, dp: 100);
		var recipe = CreateRecipe(recipeId: 155000013, dp: 600, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);

		var plan = service.CreateStartCancelPacketPlan(player, recipe, productTemplate, targetObjectId: 9006);

		Assert.Equal(CraftStartCancelPacketPlanStatus.Planned, plan.Status);
		Assert.False(plan.IsLive);
		var update = Assert.IsType<SmCraftUpdate>(plan.SelfPacket);
		var animation = Assert.IsType<SmCraftAnimation>(plan.BroadcastPacket);
		Assert.Contains("sendCancelCraft", plan.JavaSource, StringComparison.Ordinal);

		using var updateReader = new PacketBuffer(SerializeUnencryptedPayload(update));
		Assert.Equal(40001, updateReader.ReadH());
		Assert.Equal(4, updateReader.ReadC());
		Assert.Equal(100200203, updateReader.ReadD());
		Assert.Equal(0, updateReader.ReadD());
		Assert.Equal(0, updateReader.ReadD());
		Assert.Equal(0, updateReader.ReadD());
		Assert.Equal(0, updateReader.ReadD());
		Assert.Equal(1330051, updateReader.ReadD());

		using var animationReader = new PacketBuffer(SerializeUnencryptedPayload(animation));
		Assert.Equal(1112, animationReader.ReadD());
		Assert.Equal(9006, animationReader.ReadD());
		Assert.Equal(0, animationReader.ReadH());
		Assert.Equal(2, animationReader.ReadC());
	}

	[Fact]
	public void CreateStartCancelPacketPlan_MissingInputsDoesNotPlan()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1113, dp: 100);
		var recipe = CreateRecipe(recipeId: 155000014, dp: 600, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);

		var missingPlayer = service.CreateStartCancelPacketPlan(null, recipe, productTemplate, targetObjectId: 9007);
		var missingRecipe = service.CreateStartCancelPacketPlan(player, null, productTemplate, targetObjectId: 9007);
		var missingProduct = service.CreateStartCancelPacketPlan(player, recipe, null, targetObjectId: 9007);

		Assert.Equal(CraftStartCancelPacketPlanStatus.NotPlanned, missingPlayer.Status);
		Assert.Equal(CraftStartCancelPacketPlanStatus.NotPlanned, missingRecipe.Status);
		Assert.Equal(CraftStartCancelPacketPlanStatus.NotPlanned, missingProduct.Status);
		Assert.Null(missingPlayer.SelfPacket);
		Assert.Null(missingRecipe.BroadcastPacket);
		Assert.Null(missingProduct.SelfPacket);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_RejectsRideOrHideAfterDpValidation()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var rider = CreatePlayer(objectId: 1114, dp: 100);
		rider.IsInRideMode = true;
		var hidden = CreatePlayer(objectId: 1115, dp: 700);
		hidden.SetVisualState(PlayerVisualStates.Hide1);
		var recipe = CreateRecipe(recipeId: 155000015, dp: 600, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9008, templateId: 730190);

		var dpWinsBeforeStance = service.CreateStartCraftingValidationPlan(
			rider,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		rider.Dp = 700;
		var ridePlan = service.CreateStartCraftingValidationPlan(
			rider,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		var hidePlan = service.CreateStartCraftingValidationPlan(
			hidden,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.NotEnoughDp, dpWinsBeforeStance.Status);
		Assert.Equal(CraftStartValidationStatus.InvalidCurrentStance, ridePlan.Status);
		Assert.Equal(1300122, Assert.IsType<SmSystemMessage>(ridePlan.FailurePacket).MessageId);
		Assert.True(ridePlan.ShouldSendCancelCraft);
		Assert.Contains("PlayerMode.RIDE", ridePlan.JavaSource, StringComparison.Ordinal);
		Assert.Equal(CraftStartValidationStatus.InvalidCurrentStance, hidePlan.Status);
		Assert.Equal(1300122, Assert.IsType<SmSystemMessage>(hidePlan.FailurePacket).MessageId);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_RejectsInventoryFullAfterStanceValidation()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1116, dp: 700);
		player.InventoryItems = CreateFullCubeInventory(player.ObjectId);
		var ridingPlayer = CreatePlayer(objectId: 1117, dp: 700);
		ridingPlayer.IsInRideMode = true;
		ridingPlayer.InventoryItems = CreateFullCubeInventory(ridingPlayer.ObjectId);
		var recipe = CreateRecipe(recipeId: 155000016, dp: 600, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9009, templateId: 730190);

		var stanceWinsBeforeInventory = service.CreateStartCraftingValidationPlan(
			ridingPlayer,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		var inventoryFull = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.InvalidCurrentStance, stanceWinsBeforeInventory.Status);
		Assert.Equal(CraftStartValidationStatus.InventoryFull, inventoryFull.Status);
		Assert.Equal(1330037, Assert.IsType<SmSystemMessage>(inventoryFull.FailurePacket).MessageId);
		Assert.True(inventoryFull.ShouldSendCancelCraft);
		Assert.False(inventoryFull.IsReadyForNextValidation);
		Assert.Contains("isFull", inventoryFull.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateFinishProductPlan_UsesBaseProductWhenCraftDoesNotCrit()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1104, dp: 600, name: "Artisan");
		var recipe = CreateRecipe(
			recipeId: 155000005,
			dp: 0,
			productId: 152000401,
			quantity: 3,
			comboProducts: [188052501]);

		var plan = service.CreateFinishProductPlan(player, recipe, critCount: 0);

		Assert.Equal(CraftFinishProductStatus.Planned, plan.Status);
		Assert.Equal(player.ObjectId, plan.ObjectId);
		Assert.Equal(recipe.RecipeId, plan.RecipeId);
		Assert.Equal(152000401, plan.ProductItemId);
		Assert.Equal(3, plan.Quantity);
		Assert.False(plan.UsesComboProduct);
		Assert.False(plan.MarksCreatorOnEquipment);
		Assert.Null(plan.CreatorName);
	}

	[Fact]
	public void CreateFinishProductPlan_UsesComboProductAndMarksCreatorForWeapons()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1105, dp: 600, name: "Smith");
		var recipe = CreateRecipe(
			recipeId: 155000006,
			dp: 0,
			productId: 100200203,
			quantity: 1,
			comboProducts: [100200209]);

		var plan = service.CreateFinishProductPlan(player, recipe, critCount: 1);

		Assert.Equal(CraftFinishProductStatus.Planned, plan.Status);
		Assert.Equal(100200209, plan.ProductItemId);
		Assert.Equal(1, plan.Quantity);
		Assert.True(plan.UsesComboProduct);
		Assert.True(plan.MarksCreatorOnEquipment);
		Assert.Equal("Smith", plan.CreatorName);
	}

	[Fact]
	public void CreateFinishProductPlan_UsesComboIndexInJavaOrder()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1106, dp: 600);
		var recipe = CreateRecipe(
			recipeId: 155000007,
			dp: 0,
			productId: 100200203,
			quantity: 1,
			comboProducts: [100200209, 100000195]);

		var plan = service.CreateFinishProductPlan(player, recipe, critCount: 2);

		Assert.Equal(CraftFinishProductStatus.Planned, plan.Status);
		Assert.Equal(100000195, plan.ProductItemId);
		Assert.True(plan.UsesComboProduct);
	}

	[Fact]
	public void CreateFinishProductPlan_ReportsMissingComboProductConservatively()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1107, dp: 600);
		var recipe = CreateRecipe(
			recipeId: 155000008,
			dp: 0,
			productId: 100200203,
			quantity: 1);

		var plan = service.CreateFinishProductPlan(player, recipe, critCount: 1);

		Assert.Equal(CraftFinishProductStatus.MissingComboProduct, plan.Status);
		Assert.Equal(player.ObjectId, plan.ObjectId);
		Assert.Equal(recipe.RecipeId, plan.RecipeId);
		Assert.Equal(1, plan.Quantity);
		Assert.True(plan.UsesComboProduct);
		Assert.False(plan.MarksCreatorOnEquipment);
		Assert.Null(plan.CreatorName);
	}

	[Fact]
	public void CreateFinishRewardPlan_AddsCraftedEquipmentWithCreatorAndCraftedAddPacket()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1108, dp: 600, name: "Smith");
		var recipe = CreateRecipe(
			recipeId: 155000009,
			dp: 0,
			productId: 100200203,
			quantity: 1,
			comboProducts: [100200209]);
		var nextObjectId = 9000;

		var plan = service.CreateFinishRewardPlan(player, Array.Empty<InventoryItem>(), recipe, critCount: 1, () => ++nextObjectId);

		Assert.Equal(CraftFinishRewardStatus.Planned, plan.Status);
		Assert.Equal(0, plan.RemainingCount);
		Assert.False(plan.InventoryFull);
		Assert.False(plan.ShouldSendInventoryFullMessage);
		Assert.Empty(plan.UpdatedItems);
		var addedItem = Assert.Single(plan.AddedItems);
		Assert.Equal(9001, addedItem.ObjectId);
		Assert.Equal(100200209, addedItem.ItemId);
		Assert.Equal("Smith", addedItem.Creator);
		var packet = Assert.Single(plan.Packets);
		var addPacket = Assert.IsType<SmInventoryAddItem>(packet);
		Assert.Equal(SmInventoryAddItem.CraftedItem, ReadInventoryAddType(addPacket));
	}

	[Fact]
	public void CreateFinishRewardPlan_MergesStackUsingIncreaseItemCollectUpdate()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1109, dp: 600);
		var recipe = CreateRecipe(
			recipeId: 155000010,
			dp: 0,
			productId: 152000401,
			quantity: 3);
		var inventoryItems = new[]
		{
			new InventoryItem { ObjectId = 5001, ItemId = 152000401, Count = 7, OwnerId = player.ObjectId, Location = 0, Slot = 3 },
		};

		var plan = service.CreateFinishRewardPlan(player, inventoryItems, recipe, critCount: 0, () => 9001);

		Assert.Equal(CraftFinishRewardStatus.Planned, plan.Status);
		var updatedItem = Assert.Single(plan.UpdatedItems);
		Assert.Equal(10, updatedItem.Count);
		Assert.Empty(plan.AddedItems);
		var packet = Assert.Single(plan.Packets);
		var updatePacket = Assert.IsType<SmInventoryUpdateItem>(packet);
		Assert.Equal(SmInventoryUpdateItem.IncreaseItemCollect, ReadInventoryUpdateType(updatePacket));
	}

	[Fact]
	public void CreateFinishRewardPlan_ReportsInventoryFullAndPreservesPartialMerge()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1110, dp: 600);
		var recipe = CreateRecipe(
			recipeId: 155000011,
			dp: 0,
			productId: 152000401,
			quantity: 5);
		var fillerItems = Enumerable.Range(0, 26)
			.Select(index => new InventoryItem
			{
				ObjectId = 6000 + index,
				ItemId = 199000000 + index,
				Count = 1,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = index,
			});
		var inventoryItems = fillerItems
			.Prepend(new InventoryItem { ObjectId = 5002, ItemId = 152000401, Count = 8, OwnerId = player.ObjectId, Location = 0, Slot = 30 })
			.ToArray();

		var plan = service.CreateFinishRewardPlan(player, inventoryItems, recipe, critCount: 0, () => 9001);

		Assert.Equal(CraftFinishRewardStatus.InventoryFull, plan.Status);
		Assert.Equal(3, plan.RemainingCount);
		Assert.True(plan.InventoryFull);
		Assert.True(plan.ShouldSendInventoryFullMessage);
		Assert.Empty(plan.AddedItems);
		Assert.Single(plan.UpdatedItems);
		Assert.Single(plan.Packets);
	}

	[Fact]
	public void CreateFinishRewardPlan_ReportsMissingItemTemplate()
	{
		var service = CreateService(out _, itemTemplates: null);
		var player = CreatePlayer(objectId: 1111, dp: 600);
		var recipe = CreateRecipe(recipeId: 155000012, dp: 0, productId: 152000401, quantity: 1);

		var plan = service.CreateFinishRewardPlan(player, Array.Empty<InventoryItem>(), recipe, critCount: 0, () => 1);

		Assert.Equal(CraftFinishRewardStatus.MissingItemTemplate, plan.Status);
		Assert.Empty(plan.Packets);
		Assert.Empty(plan.AddedItems);
		Assert.Empty(plan.UpdatedItems);
	}

	private static CraftService CreateService(out CapturingConnectionRegistry registry, ItemTemplateTable? itemTemplates = null)
	{
		registry = new CapturingConnectionRegistry();
		var resourceStats = new WorldNpcResourceStatsService(
			new WorldNpcLifeStatsService(new WorldNpcDeathDropWorkflowService(null!, null!)),
			registry,
			new PlayerVisualStatsUpdateService(registry));
		return new CraftService(resourceStats, itemTemplates);
	}

	private static void AssertVisualStatsUpdate(WorldNpcResourceChangeResult change)
	{
		Assert.NotNull(change.VisualStatsUpdate);
		Assert.Equal(PlayerVisualStatsUpdateStatus.StatsAndSpeedSent, change.VisualStatsUpdate.Status);
		Assert.True(change.VisualStatsUpdate.StatsPacketSent);
		Assert.NotNull(change.VisualStatsUpdate.StatsPacket);
		Assert.NotNull(change.VisualStatsUpdate.SpeedSnapshot);
		Assert.Equal(6.0f, change.VisualStatsUpdate.SpeedSnapshot.MovementSpeed);
		Assert.NotNull(change.VisualStatsUpdate.SpeedPacket);
		Assert.Equal(1, change.VisualStatsUpdate.SpeedBroadcastCount);
	}

	private static Player CreatePlayer(int objectId, int dp, string name = "Crafter")
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Dp = dp,
			IsOnline = true,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}

	private static RecipeTemplateSummary CreateRecipe(
		int recipeId,
		int dp,
		int productId = 100000001,
		int quantity = 1,
		IReadOnlyList<int>? comboProducts = null,
		int skillId = CraftStartValidationPlan.MorphSubstancesSkillId)
	{
		return new RecipeTemplateSummary(
			recipeId,
			0,
			skillId,
			"PC_ALL",
			0,
			dp,
			0,
			productId,
			quantity,
			comboProducts);
	}

	private static WorldNpc CreateTarget(int objectId, int templateId)
	{
		var template = new NpcTemplateSummary(
			templateId,
			"Craft Tool",
			NameId: 0,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "NONE",
			Type: "STATIC");
		return new WorldNpc(objectId, templateId, template, new WorldPosition(210010000, 10, 20, 30, 0));
	}

	private static IReadOnlyList<InventoryItem> CreateFullCubeInventory(int ownerId)
	{
		return Enumerable.Range(0, 27)
			.Select(index => new InventoryItem
			{
				ObjectId = 7000 + index,
				ItemId = 199100000 + index,
				Count = 1,
				OwnerId = ownerId,
				Location = 0,
				Slot = index,
			})
			.ToArray();
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(
				100200203,
				"Practice Sword",
				0,
				0,
				1,
				"SWORD",
				"ITEM",
				"COMMON",
				"PC_ALL",
				1,
				1,
				1),
			new ItemTemplateSummary(
				100200209,
				"Critical Sword",
				0,
				0,
				1,
				"SWORD",
				"ITEM",
				"COMMON",
				"PC_ALL",
				1,
				1,
				1),
			new ItemTemplateSummary(
				100000195,
				"Second Critical Sword",
				0,
				0,
				1,
				"SWORD",
				"ITEM",
				"COMMON",
				"PC_ALL",
				1,
				1,
				1),
			new ItemTemplateSummary(
				152000401,
				"Crafted Material",
				0,
				0,
				1,
				"QUEST",
				"ITEM",
				"COMMON",
				"PC_ALL",
				10,
				1,
				0),
		]);
	}

	private static int ReadInventoryAddType(SmInventoryAddItem packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		return reader.ReadH();
	}

	private static int ReadInventoryUpdateType(SmInventoryUpdateItem packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		reader.ReadD();
		reader.ReadS();
		var blobSize = reader.ReadH();
		reader.ReadB(blobSize);
		return reader.ReadH();
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<BroadcastRecord> Broadcasts { get; } = [];

		public List<PacketDelivery> SentPackets { get; } = [];

		public List<GameServerPacket> PacketOrder { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			PacketOrder.Add(packet);
			SentPackets.Add(new PacketDelivery(playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			PacketOrder.Add(packet);
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			return Task.FromResult(1);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);
}
