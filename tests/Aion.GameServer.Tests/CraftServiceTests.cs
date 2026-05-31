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
		player.Recipes = [recipe.RecipeId];
		player.Skills = [CreateSkill(recipe.SkillId, recipe.SkillPoint)];
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
		player.Recipes = [recipe.RecipeId];
		player.Skills = [CreateSkill(recipe.SkillId, recipe.SkillPoint)];
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
		player.Recipes = [recipe.RecipeId];
		player.Skills = [CreateSkill(recipe.SkillId, recipe.SkillPoint)];
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
	public void CreateStartFailureOrchestrationPlan_OrdersFailurePacketBeforeCancelPackets()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1131, dp: 700);
		player.InventoryItems = CreateFullCubeInventory(player.ObjectId);
		var recipe = CreateRecipe(recipeId: 155000024, dp: 600, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9017, templateId: 730190);
		var validation = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		var cancel = service.CreateStartCancelPacketPlan(player, recipe, productTemplate, target.ObjectId);

		var plan = service.CreateStartFailureOrchestrationPlan(validation, cancel);

		Assert.Equal(CraftStartFailureOrchestrationStatus.Planned, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Same(validation, plan.ValidationPlan);
		Assert.Same(cancel, plan.CancelPlan);
		Assert.Collection(
			plan.OrderedPackets,
			packet => Assert.Same(validation.FailurePacket, packet),
			packet => Assert.Same(cancel.SelfPacket, packet),
			packet => Assert.Same(cancel.BroadcastPacket, packet));
		Assert.Equal(1330037, Assert.IsType<SmSystemMessage>(plan.OrderedPackets[0]).MessageId);
		Assert.IsType<SmCraftUpdate>(plan.OrderedPackets[1]);
		Assert.IsType<SmCraftAnimation>(plan.OrderedPackets[2]);
		Assert.Contains("checkCraft", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartFailureOrchestrationPlan_PlansCancelPacketsForAuditOnlyFailures()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1132, dp: 100);
		var recipe = CreateRecipe(recipeId: 155000025, dp: 600, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9018, templateId: 730190);
		var validation = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		var cancel = service.CreateStartCancelPacketPlan(player, recipe, productTemplate, target.ObjectId);

		var plan = service.CreateStartFailureOrchestrationPlan(validation, cancel);

		Assert.Equal(CraftStartValidationStatus.NotEnoughDp, validation.Status);
		Assert.Null(validation.FailurePacket);
		Assert.Equal(CraftStartFailureOrchestrationStatus.Planned, plan.Status);
		Assert.Collection(
			plan.OrderedPackets,
			packet => Assert.Same(cancel.SelfPacket, packet),
			packet => Assert.Same(cancel.BroadcastPacket, packet));
	}

	[Fact]
	public void CreateStartFailureOrchestrationPlan_DoesNotPlanForReadyValidation()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1133, dp: 700);
		var recipe = CreateRecipe(recipeId: 155000026, dp: 600, productId: 100200203, skillId: 40001);
		player.Recipes = [recipe.RecipeId];
		player.Skills = [CreateSkill(recipe.SkillId, recipe.SkillPoint)];
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9019, templateId: 730190);
		var validation = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		var cancel = service.CreateStartCancelPacketPlan(player, recipe, productTemplate, target.ObjectId);

		var plan = service.CreateStartFailureOrchestrationPlan(validation, cancel);

		Assert.Equal(CraftStartValidationStatus.ReadyForNextValidation, validation.Status);
		Assert.Equal(CraftStartFailureOrchestrationStatus.NotPlanned, plan.Status);
		Assert.Empty(plan.OrderedPackets);
		Assert.Contains("checkCraft returned true", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartFailureOrchestrationPlan_ReportsMissingCancelPrerequisites()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1134, dp: 100);
		var recipe = CreateRecipe(recipeId: 155000027, dp: 600, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9020, templateId: 730190);
		var validation = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		var cancel = service.CreateStartCancelPacketPlan(player, recipe, productTemplate: null, target.ObjectId);

		var plan = service.CreateStartFailureOrchestrationPlan(validation, cancel);

		Assert.Equal(CraftStartValidationStatus.NotEnoughDp, validation.Status);
		Assert.Equal(CraftStartCancelPacketPlanStatus.NotPlanned, cancel.Status);
		Assert.Equal(CraftStartFailureOrchestrationStatus.CancelNotPlanned, plan.Status);
		Assert.Empty(plan.OrderedPackets);
		Assert.Contains("sendCancelCraft", plan.JavaSource, StringComparison.Ordinal);
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
	public void CreateStartCraftingValidationPlan_RejectsMissingKnownRecipeAfterInventoryValidation()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var player = CreatePlayer(objectId: 1118, dp: 700);
		var fullInventoryPlayer = CreatePlayer(objectId: 1119, dp: 700);
		fullInventoryPlayer.InventoryItems = CreateFullCubeInventory(fullInventoryPlayer.ObjectId);
		var recipe = CreateRecipe(recipeId: 155000017, dp: 600, productId: 100200203, skillId: 40001);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9010, templateId: 730190);

		var inventoryWinsBeforeRecipe = service.CreateStartCraftingValidationPlan(
			fullInventoryPlayer,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		var missingRecipe = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.InventoryFull, inventoryWinsBeforeRecipe.Status);
		Assert.Equal(CraftStartValidationStatus.MissingKnownRecipe, missingRecipe.Status);
		Assert.Equal(1330043, Assert.IsType<SmSystemMessage>(missingRecipe.FailurePacket).MessageId);
		Assert.True(missingRecipe.ShouldSendCancelCraft);
		Assert.Contains("isRecipePresent", missingRecipe.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_RejectsCraftCooldownAfterRecipeValidation()
	{
		var service = CreateService(out _, CreateItemTemplates());
		var missingRecipePlayer = CreatePlayer(objectId: 1120, dp: 700);
		missingRecipePlayer.CraftCooldowns = new Dictionary<int, long> { [77] = 999999 };
		var player = CreatePlayer(objectId: 1121, dp: 700);
		player.Recipes = [155000018];
		player.CraftCooldowns = new Dictionary<int, long> { [77] = 999999 };
		var recipe = CreateRecipe(recipeId: 155000018, dp: 600, productId: 100200203, skillId: 40001, craftDelayId: 77);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9011, templateId: 730190);

		var recipeWinsBeforeCooldown = service.CreateStartCraftingValidationPlan(
			missingRecipePlayer,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		var cooldown = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.MissingKnownRecipe, recipeWinsBeforeCooldown.Status);
		Assert.Equal(CraftStartValidationStatus.CraftCooldownActive, cooldown.Status);
		Assert.Equal(1300494, Assert.IsType<SmSystemMessage>(cooldown.FailurePacket).MessageId);
		Assert.True(cooldown.ShouldSendCancelCraft);
		Assert.Contains("hasCooldown", cooldown.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_RejectsMissingCraftSkillAfterCooldownValidation()
	{
		var service = CreateService(out _, CreateItemTemplates(), CreateSkillTemplates());
		var cooldownPlayer = CreatePlayer(objectId: 1122, dp: 700);
		cooldownPlayer.Recipes = [155000019];
		cooldownPlayer.CraftCooldowns = new Dictionary<int, long> { [78] = 999999 };
		var player = CreatePlayer(objectId: 1123, dp: 700);
		player.Recipes = [155000019];
		var recipe = CreateRecipe(recipeId: 155000019, dp: 600, productId: 100200203, skillId: 40001, skillPoint: 200, craftDelayId: 78);
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9012, templateId: 730190);

		var cooldownWinsBeforeSkill = service.CreateStartCraftingValidationPlan(
			cooldownPlayer,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);
		var missingSkill = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.CraftCooldownActive, cooldownWinsBeforeSkill.Status);
		Assert.Equal(CraftStartValidationStatus.MissingCraftSkill, missingSkill.Status);
		Assert.Equal(1330042, Assert.IsType<SmSystemMessage>(missingSkill.FailurePacket).MessageId);
		Assert.Equal(200, missingSkill.RequiredSkillPoint);
		Assert.Equal(0, missingSkill.CurrentSkillLevel);
		Assert.True(missingSkill.ShouldSendCancelCraft);
		Assert.Contains("isSkillPresent", missingSkill.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_RejectsLowCraftSkillAfterPresenceValidation()
	{
		var service = CreateService(out _, CreateItemTemplates(), CreateSkillTemplates());
		var player = CreatePlayer(objectId: 1124, dp: 700);
		var recipe = CreateRecipe(recipeId: 155000020, dp: 600, productId: 100200203, skillId: 40001, skillPoint: 200);
		player.Recipes = [recipe.RecipeId];
		player.Skills = [CreateSkill(recipe.SkillId, skillLevel: 100)];
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9013, templateId: 730190);

		var plan = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CraftStartValidationStatus.CraftSkillTooLow, plan.Status);
		Assert.Equal(1330044, Assert.IsType<SmSystemMessage>(plan.FailurePacket).MessageId);
		Assert.Equal(200, plan.RequiredSkillPoint);
		Assert.Equal(100, plan.CurrentSkillLevel);
		Assert.True(plan.ShouldSendCancelCraft);
		Assert.False(plan.IsReadyForNextValidation);
		Assert.Contains("getSkillLevel", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_RejectsMissingSelectedComponentAfterSkillValidation()
	{
		var service = CreateService(out _, CreateItemTemplates(), CreateSkillTemplates());
		var lowSkillPlayer = CreatePlayer(objectId: 1125, dp: 700);
		var player = CreatePlayer(objectId: 1126, dp: 700);
		var recipe = CreateRecipe(
			recipeId: 155000021,
			dp: 600,
			productId: 100200203,
			skillId: 40001,
			skillPoint: 200,
			componentGroups: [CreateComponentGroup((152000901, 3))]);
		lowSkillPlayer.Recipes = [recipe.RecipeId];
		lowSkillPlayer.Skills = [CreateSkill(recipe.SkillId, skillLevel: 100)];
		player.Recipes = [recipe.RecipeId];
		player.Skills = [CreateSkill(recipe.SkillId, skillLevel: 200)];
		player.InventoryItems = [CreateInventoryItem(objectId: 8001, itemId: 152000901, count: 2)];
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9014, templateId: 730190);
		var selectedMaterials = new Dictionary<int, long> { [152000901] = 3 };

		var skillWinsBeforeMaterials = service.CreateStartCraftingValidationPlan(
			lowSkillPlayer,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false,
			selectedMaterials);
		var missingComponent = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false,
			selectedMaterials);

		Assert.Equal(CraftStartValidationStatus.CraftSkillTooLow, skillWinsBeforeMaterials.Status);
		Assert.Equal(CraftStartValidationStatus.MissingComponentItem, missingComponent.Status);
		Assert.Equal(1330047, Assert.IsType<SmSystemMessage>(missingComponent.FailurePacket).MessageId);
		Assert.Equal(152000901, missingComponent.MissingComponentItemId);
		Assert.Equal(3, missingComponent.MissingComponentRequiredCount);
		Assert.Equal(2, missingComponent.MissingComponentAvailableCount);
		Assert.True(missingComponent.ShouldSendCancelCraft);
		Assert.Contains("component group", missingComponent.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_ValidatesOnlySelectedComponentGroup()
	{
		var service = CreateService(out _, CreateItemTemplates(), CreateSkillTemplates());
		var player = CreatePlayer(objectId: 1127, dp: 700);
		var recipe = CreateRecipe(
			recipeId: 155000022,
			dp: 600,
			productId: 100200203,
			skillId: 40001,
			skillPoint: 200,
			componentGroups:
			[
				CreateComponentGroup((152000901, 5)),
				CreateComponentGroup((152000902, 1)),
			]);
		player.Recipes = [recipe.RecipeId];
		player.Skills = [CreateSkill(recipe.SkillId, skillLevel: 200)];
		player.InventoryItems = [CreateInventoryItem(objectId: 8002, itemId: 152000902, count: 1)];
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9015, templateId: 730190);

		var plan = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false,
			selectedMaterialData: new Dictionary<int, long> { [152000902] = 1 });

		Assert.Equal(CraftStartValidationStatus.ReadyForNextValidation, plan.Status);
		Assert.Equal(0, plan.MissingComponentItemId);
		Assert.True(plan.IsReadyForNextValidation);
		Assert.Contains("material consumption", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateStartCraftingValidationPlan_RejectsMissingBonusItemAfterMaterialValidation()
	{
		var service = CreateService(out _, CreateItemTemplates(), CreateSkillTemplates());
		var missingMaterialPlayer = CreatePlayer(objectId: 1128, dp: 700);
		var player = CreatePlayer(objectId: 1129, dp: 700);
		var readyPlayer = CreatePlayer(objectId: 1130, dp: 700);
		var recipe = CreateRecipe(
			recipeId: 155000023,
			dp: 600,
			productId: 100200203,
			skillId: 40001,
			skillPoint: 200,
			componentGroups: [CreateComponentGroup((152000901, 1))]);
		foreach (var candidate in new[] { missingMaterialPlayer, player, readyPlayer })
		{
			candidate.Recipes = [recipe.RecipeId];
			candidate.Skills = [CreateSkill(recipe.SkillId, skillLevel: 200)];
		}

		player.InventoryItems = [CreateInventoryItem(objectId: 8003, itemId: 152000901, count: 1)];
		readyPlayer.InventoryItems =
		[
			CreateInventoryItem(objectId: 8004, itemId: 152000901, count: 1),
			CreateInventoryItem(objectId: 8005, itemId: 169401081, count: 1),
		];
		var productTemplate = CreateItemTemplates().GetItemTemplate(100200203);
		var target = CreateTarget(objectId: 9016, templateId: 730190);
		var selectedMaterials = new Dictionary<int, long> { [152000901] = 1 };

		var materialWinsBeforeBonus = service.CreateStartCraftingValidationPlan(
			missingMaterialPlayer,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false,
			selectedMaterials,
			craftType: 1);
		var missingBonus = service.CreateStartCraftingValidationPlan(
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false,
			selectedMaterials,
			craftType: 1);
		var ready = service.CreateStartCraftingValidationPlan(
			readyPlayer,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false,
			selectedMaterials,
			craftType: 1);

		Assert.Equal(CraftStartValidationStatus.MissingComponentItem, materialWinsBeforeBonus.Status);
		Assert.Equal(CraftStartValidationStatus.MissingBonusItem, missingBonus.Status);
		Assert.Equal(169401081, missingBonus.MissingComponentItemId);
		Assert.Equal(1, missingBonus.MissingComponentRequiredCount);
		Assert.Equal(0, missingBonus.MissingComponentAvailableCount);
		Assert.Equal(1330046, Assert.IsType<SmSystemMessage>(missingBonus.FailurePacket).MessageId);
		Assert.True(missingBonus.ShouldSendCancelCraft);
		Assert.Contains("getBonusReqItem", missingBonus.JavaSource, StringComparison.Ordinal);
		Assert.Equal(CraftStartValidationStatus.ReadyForNextValidation, ready.Status);
		Assert.True(ready.IsReadyForNextValidation);
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

	private static CraftService CreateService(
		out CapturingConnectionRegistry registry,
		ItemTemplateTable? itemTemplates = null,
		SkillTemplateTable? skillTemplates = null)
	{
		registry = new CapturingConnectionRegistry();
		var resourceStats = new WorldNpcResourceStatsService(
			new WorldNpcLifeStatsService(new WorldNpcDeathDropWorkflowService(null!, null!)),
			registry,
			new PlayerVisualStatsUpdateService(registry));
		return new CraftService(resourceStats, itemTemplates, skillTemplates);
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
		int skillId = CraftStartValidationPlan.MorphSubstancesSkillId,
		int skillPoint = 0,
		int? craftDelayId = null,
		int? craftDelayTime = null,
		IReadOnlyList<RecipeComponentDataSummary>? componentGroups = null)
	{
		return new RecipeTemplateSummary(
			recipeId,
			0,
			skillId,
			"PC_ALL",
			skillPoint,
			dp,
			0,
			productId,
			quantity,
			comboProducts,
			craftDelayId,
			craftDelayTime,
			componentGroups);
	}

	private static PlayerSkill CreateSkill(int skillId, int skillLevel)
	{
		return new PlayerSkill
		{
			SkillId = skillId,
			SkillLevel = skillLevel,
		};
	}

	private static RecipeComponentDataSummary CreateComponentGroup(params (int ItemId, long Quantity)[] components)
	{
		return new RecipeComponentDataSummary(
			components
				.Select(component => new RecipeComponentSummary(component.ItemId, component.Quantity))
				.ToArray());
	}

	private static InventoryItem CreateInventoryItem(int objectId, int itemId, long count)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			Location = 0,
		};
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
			new ItemTemplateSummary(
				152000901,
				"Material A",
				730900,
				0,
				1,
				"MATERIAL",
				"ITEM",
				"COMMON",
				"PC_ALL",
				10,
				1,
				0),
			new ItemTemplateSummary(
				152000902,
				"Material B",
				730901,
				0,
				1,
				"MATERIAL",
				"ITEM",
				"COMMON",
				"PC_ALL",
				10,
				1,
				0),
			new ItemTemplateSummary(
				169401081,
				"Cooking Bonus",
				731081,
				0,
				1,
				"MATERIAL",
				"ITEM",
				"COMMON",
				"PC_ALL",
				10,
				1,
				0),
		]);
	}

	private static SkillTemplateTable CreateSkillTemplates()
	{
		return new SkillTemplateTable(
		[
			new SkillTemplateSummary(
				40001,
				"Weapon Smithing",
				12345,
				1,
				"CRAFT",
				"CRAFT",
				"CRAFT",
				"NONE",
				0,
				0),
			new SkillTemplateSummary(
				CraftStartValidationPlan.MorphSubstancesSkillId,
				"Morph Substances",
				12346,
				1,
				"MORPH",
				"MORPH",
				"CRAFT",
				"NONE",
				0,
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
