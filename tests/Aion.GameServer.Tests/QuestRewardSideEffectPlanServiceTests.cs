using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

public sealed class QuestRewardSideEffectPlanServiceTests
{
	[Fact]
	public void CreateTitleRewardPlan_PlansQuestTitlePersistenceAndPackets()
	{
		var player = CreatePlayer(objectId: 3100, race: "ELYOS");
		var titles = CreateTitles(new TitleTemplateSummary(5, 412994, "quest title", "ELYOS", Array.Empty<ItemStatModifier>()));

		var plan = QuestRewardSideEffectPlanService.CreateTitleRewardPlan(player, 5, titles);

		Assert.Equal(QuestTitleRewardStatus.Applied, plan.Status);
		Assert.True(plan.Applied);
		Assert.Equal(player.ObjectId, plan.ObjectId);
		Assert.Equal(5, plan.TitleId);
		Assert.Equal(new PlayerTitle(5, 0), plan.Title);
		Assert.Same(titles.GetTitleTemplate(5), plan.TitleTemplate);
		Assert.False(plan.JavaWouldThrow);
		Assert.True(plan.RequiresImmediatePersistence);
		Assert.True(plan.RequiresExpireRegistration);
		Assert.Equal(ChatUtil.L10n(412994), plan.TitleName);
		Assert.Equal(
		[
			QuestRewardPacketIntent.QuestTitleSystemMessage,
			QuestRewardPacketIntent.FullTitleInfo,
		], plan.PacketIntents);
		Assert.Equal("TitleList.addTitle(title, true, 0)", plan.JavaSource);
		Assert.Empty(player.Titles);
	}

	[Fact]
	public void CreateTitleRewardPlan_PreservesInvalidRaceDuplicateAndInvalidTemplateBranches()
	{
		var templates = CreateTitles(new TitleTemplateSummary(7, 412997, "asmo title", "ASMODIANS", Array.Empty<ItemStatModifier>()));
		var raceMismatch = QuestRewardSideEffectPlanService.CreateTitleRewardPlan(
			CreatePlayer(objectId: 3101, race: "ELYOS"),
			7,
			templates);
		var duplicate = QuestRewardSideEffectPlanService.CreateTitleRewardPlan(
			CreatePlayer(objectId: 3102, race: "ASMODIANS", titles: [new PlayerTitle(7, 0)]),
			7,
			templates);
		var invalidTemplate = QuestRewardSideEffectPlanService.CreateTitleRewardPlan(
			CreatePlayer(objectId: 3103, race: "ASMODIANS"),
			99,
			templates);

		Assert.Equal(QuestTitleRewardStatus.InvalidRace, raceMismatch.Status);
		Assert.False(raceMismatch.JavaWouldThrow);
		Assert.False(raceMismatch.RequiresImmediatePersistence);
		Assert.Equal("This title is not available for your race.", raceMismatch.PlainTextMessage);
		Assert.Equal([QuestRewardPacketIntent.PlainTextRaceFailureMessage], raceMismatch.PacketIntents);

		Assert.Equal(QuestTitleRewardStatus.AlreadyKnown, duplicate.Status);
		Assert.False(duplicate.RequiresImmediatePersistence);
		Assert.Equal([QuestRewardPacketIntent.TooltipLearnedTitle], duplicate.PacketIntents);

		Assert.Equal(QuestTitleRewardStatus.InvalidTitle, invalidTemplate.Status);
		Assert.True(invalidTemplate.JavaWouldThrow);
		Assert.Empty(invalidTemplate.PacketIntents);
	}

	[Fact]
	public void CreateCubeExpansionPlan_PlansQuestExpandWithoutMutatingPlayer()
	{
		var player = CreatePlayer(objectId: 3104, questExpands: 1, npcExpands: 2, itemExpands: 3);

		var plan = QuestRewardSideEffectPlanService.CreateCubeExpansionPlan(player, cubeExpansionLimit: 11);

		Assert.Equal(QuestExpansionRewardStatus.Applied, plan.Status);
		Assert.True(plan.Applied);
		Assert.Equal(QuestExpansionRewardKind.Cube, plan.Kind);
		Assert.Equal(player.ObjectId, plan.ObjectId);
		Assert.Equal(7, plan.RequestedExpansionLevel);
		Assert.Equal(11, plan.ExpansionLimit);
		Assert.Equal(1, plan.PreviousExpansionCount);
		Assert.Equal(2, plan.NewExpansionCount);
		Assert.Equal(81, plan.PreviousSlotLimit);
		Assert.Equal(90, plan.NewSlotLimit);
		Assert.True(plan.RequiresPlayerPersistence);
		Assert.Equal(
		[
			QuestRewardPacketIntent.InventorySizeExtended,
			QuestRewardPacketIntent.CubeUpdate,
		], plan.PacketIntents);
		Assert.Equal(1, player.QuestExpands);
	}

	[Fact]
	public void CreateCubeExpansionPlan_RecordsJavaCannotExpandBoundary()
	{
		var player = CreatePlayer(objectId: 3105, questExpands: 5, npcExpands: 3, itemExpands: 3);

		var plan = QuestRewardSideEffectPlanService.CreateCubeExpansionPlan(player, cubeExpansionLimit: 11);

		Assert.Equal(QuestExpansionRewardStatus.CannotExpand, plan.Status);
		Assert.False(plan.Applied);
		Assert.Equal(12, plan.RequestedExpansionLevel);
		Assert.Equal(11, plan.ExpansionLimit);
		Assert.Equal(5, plan.PreviousExpansionCount);
		Assert.Equal(5, plan.NewExpansionCount);
		Assert.False(plan.RequiresPlayerPersistence);
		Assert.Equal([QuestRewardPacketIntent.CannotExpandSystemMessage], plan.PacketIntents);
	}

	[Fact]
	public void CreateWarehouseExpansionPlan_PlansBonusExpansionAndPackets()
	{
		var player = CreatePlayer(objectId: 3106, warehouseNpcExpands: 2, warehouseBonusExpands: 3);

		var plan = QuestRewardSideEffectPlanService.CreateWarehouseExpansionPlan(player);

		Assert.Equal(QuestExpansionRewardStatus.Applied, plan.Status);
		Assert.Equal(QuestExpansionRewardKind.Warehouse, plan.Kind);
		Assert.Equal(6, plan.RequestedExpansionLevel);
		Assert.Equal(11, plan.ExpansionLimit);
		Assert.Equal(3, plan.PreviousExpansionCount);
		Assert.Equal(4, plan.NewExpansionCount);
		Assert.Equal(64, plan.PreviousSlotLimit);
		Assert.Equal(72, plan.NewSlotLimit);
		Assert.True(plan.RequiresPlayerPersistence);
		Assert.Equal(
		[
			QuestRewardPacketIntent.WarehouseSizeExtended,
			QuestRewardPacketIntent.RegularWarehouseInfo,
		], plan.PacketIntents);
		Assert.Equal(3, player.WarehouseBonusExpands);
	}

	[Fact]
	public void CreateWarehouseExpansionPlan_RecordsJavaCannotExpandBoundary()
	{
		var player = CreatePlayer(objectId: 3107, warehouseNpcExpands: 7, warehouseBonusExpands: 4);

		var plan = QuestRewardSideEffectPlanService.CreateWarehouseExpansionPlan(player);

		Assert.Equal(QuestExpansionRewardStatus.CannotExpand, plan.Status);
		Assert.Equal(12, plan.RequestedExpansionLevel);
		Assert.Equal(11, plan.ExpansionLimit);
		Assert.Equal(4, plan.PreviousExpansionCount);
		Assert.Equal(4, plan.NewExpansionCount);
		Assert.Equal(112, plan.PreviousSlotLimit);
		Assert.Equal(112, plan.NewSlotLimit);
		Assert.False(plan.RequiresPlayerPersistence);
		Assert.Equal([QuestRewardPacketIntent.CannotExpandSystemMessage], plan.PacketIntents);
	}

	[Fact]
	public void CreateGpRewardPlan_AppliesRateAndPlansPacketsWithoutMutatingPlayer()
	{
		var player = CreatePlayer(objectId: 3108, membership: 1, gp: 200);

		var result = QuestRewardSideEffectPlanService.CreateGpRewardPlan(player, rewardGp: 75, gpRates: [1f, 2f]);

		Assert.Equal(QuestGpRewardStatus.Applied, result.Status);
		Assert.Equal(75, result.RewardGp);
		Assert.Equal(150, result.AppliedRewardGp);
		Assert.Equal(200, result.PreviousGp);
		Assert.Equal(350, result.CurrentGp);
		Assert.NotNull(result.GloryPointsPlan);
		Assert.Equal(150, result.GloryPointsPlan!.Added);
		Assert.True(result.GloryPointsPlan.AddsDailyWeeklyStats);
		Assert.Equal(2, result.GloryPointsPlan.PlayerPackets.Count);
		Assert.Equal(200, player.AbyssRank.Gp);
		Assert.Equal(0, player.AbyssRank.DailyGp);
		Assert.Equal(0, player.AbyssRank.WeeklyGp);
	}

	private static Player CreatePlayer(
		int objectId,
		string race = "ELYOS",
		IReadOnlyList<PlayerTitle>? titles = null,
		int questExpands = 0,
		int npcExpands = 0,
		int itemExpands = 0,
		int warehouseNpcExpands = 0,
		int warehouseBonusExpands = 0,
		byte membership = 0,
		int gp = 0)
	{
		return new Player
		{
			ObjectId = objectId,
			Race = race,
			AccountMembership = membership,
			AbyssRank = PlayerAbyssRank.Default() with { Gp = gp },
			Titles = titles ?? Array.Empty<PlayerTitle>(),
			QuestExpands = questExpands,
			NpcExpands = npcExpands,
			ItemExpands = itemExpands,
			WarehouseNpcExpands = warehouseNpcExpands,
			WarehouseBonusExpands = warehouseBonusExpands,
		};
	}

	private static TitleTemplateTable CreateTitles(params TitleTemplateSummary[] titles)
	{
		return new TitleTemplateTable(titles);
	}
}
