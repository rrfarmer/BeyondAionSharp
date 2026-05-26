using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishOperationPlanServiceTests
{
	[Fact]
	public void CreatePlan_ComposesJavaQuestFinishOrderingWithoutLiveSideEffects()
	{
		var now = new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero);
		var questState = new PlayerQuestState(
			QuestId: 1001,
			Status: "REWARD",
			QuestVars: 0x123456,
			Flags: 2,
			CompleteCount: 0);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			questState,
			new NearbyQuestTemplateSummary(1001),
			PlayerNpcFactionsSnapshot.Empty,
			now,
			CreateOptions("UTC"));

		Assert.True(plan.Applied);
		Assert.NotNull(plan.QuestState);
		Assert.Equal("COMPLETE", plan.QuestState.Status);
		Assert.Equal(0, plan.QuestState.QuestVars);
		Assert.Equal(1, plan.QuestState.CompleteCount);
		Assert.Equal(now, plan.QuestState.CompleteTime);
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardMutationPlaceholder,
			QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
			QuestFinishOperationAction.QuestUpdatePacket,
			QuestFinishOperationAction.QuestCompletedCallback,
			QuestFinishOperationAction.NearbyQuestRefresh,
			QuestFinishOperationAction.DeferredQuestPersistence,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal([1, 2, 3, 4, 5, 6, 7], plan.Descriptors.Select(descriptor => descriptor.Order));
	}

	[Fact]
	public void CreatePlan_ComposesNpcFactionCompletionAfterCallbackAndBeforeNearbyRefresh()
	{
		var now = new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero);
		var npcFactions = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Start,
				QuestId: 35007),
		]);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(35007, "REWARD", QuestVars: 4, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(35007, NpcFactionId: 2),
			npcFactions,
			now,
			CreateOptions("UTC"));

		Assert.True(plan.Applied);
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardMutationPlaceholder,
			QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
			QuestFinishOperationAction.QuestUpdatePacket,
			QuestFinishOperationAction.QuestCompletedCallback,
			QuestFinishOperationAction.NpcFactionCompletion,
			QuestFinishOperationAction.NearbyQuestRefresh,
			QuestFinishOperationAction.DeferredQuestPersistence,
			QuestFinishOperationAction.DeferredNpcFactionPersistence,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.True(plan.NpcFactions.TryGetFaction(2, out var faction));
		Assert.NotNull(faction);
		Assert.Equal(PlayerNpcFactionQuestState.Complete, faction.State);
		Assert.Equal(new DateTimeOffset(2026, 5, 25, 9, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), faction.TimeEpochSeconds);
	}

	[Fact]
	public void CreatePlan_ComposesRewardProjectionBeforeStateMutation()
	{
		var now = new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero);
		var questState = new PlayerQuestState(
			QuestId: 1001,
			Status: "REWARD",
			QuestVars: 0x123456,
			Flags: 2,
			CompleteCount: 0);
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 2,
			HasItemRewards: true,
			HasNonItemRewards: true,
			IsChallengeTask: true,
			WorkItems:
			[
				new QuestFinishRewardWorkItem(ItemId: 182400001, Count: 3),
			]);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			questState,
			new NearbyQuestTemplateSummary(1001),
			PlayerNpcFactionsSnapshot.Empty,
			now,
			CreateOptions("UTC"),
			rewardProjection);

		Assert.True(plan.Applied);
		Assert.NotNull(plan.QuestState);
		Assert.Equal("COMPLETE", plan.QuestState.Status);
		Assert.Equal(0, plan.QuestState.RewardGroup);
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardGroupCorrection,
			QuestFinishOperationAction.ItemRewardPlaceholder,
			QuestFinishOperationAction.NonItemRewardPlaceholder,
			QuestFinishOperationAction.ChallengeTaskCompletionPlaceholder,
			QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
			QuestFinishOperationAction.QuestUpdatePacket,
			QuestFinishOperationAction.QuestCompletedCallback,
			QuestFinishOperationAction.NearbyQuestRefresh,
			QuestFinishOperationAction.DeferredQuestPersistence,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal(Enumerable.Range(1, 10), plan.Descriptors.Select(descriptor => descriptor.Order));
		var workItemDescriptor = Assert.Single(
			plan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder);
		Assert.Equal(182400001, workItemDescriptor.ItemId);
		Assert.Equal(3, workItemDescriptor.Count);
	}

	[Fact]
	public void CreatePlan_ComposesDialogAutoRewardGuardIntentWithExplicitProjectionWithoutLiveSideEffects()
	{
		var guardPlan = QuestDialogAutoRewardGuardPlanService.CreatePlan(
			new QuestDialogAutoRewardGuardInput(
				PlayerObjectId: 77,
				TargetObjectId: 0,
				DialogActionId: 108,
				QuestId: 1001,
				QuestTemplateExists: true,
				QuestTemplateCanReport: true));
		Assert.True(guardPlan.Planned);
		Assert.False(guardPlan.IsLive);

		var operationPlan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0x12, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001, QuestCategory: "QUEST"),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			new QuestFinishRewardTemplateProjection(
				RewardGroupCount: 1,
				HasNonItemRewards: true,
				NonItemProjection: new QuestFinishRewardNonItemTemplateProjection(Experience: 400),
				DialogActionId: guardPlan.DialogActionId));

		Assert.True(operationPlan.Applied);
		Assert.All(operationPlan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Contains(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardProjection);
		Assert.Contains(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardPlaceholder);
		Assert.Contains(operationPlan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.QuestStateMutation);
		Assert.Equal(108, guardPlan.DialogActionId);
		Assert.Equal("COMPLETE", operationPlan.QuestState?.Status);
	}

	[Fact]
	public void CreatePlan_ComposesDetailedRewardItemProjectionBeforeCoarsePlaceholder()
	{
		var now = new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero);
		var questState = new PlayerQuestState(
			QuestId: 1001,
			Status: "REWARD",
			QuestVars: 0x123456,
			Flags: 2,
			CompleteCount: 2);
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 2,
			HasItemRewards: true,
			ItemProjection: new QuestFinishRewardItemTemplateProjection(
				RewardGroups:
				[
					new QuestFinishRewardGroupProjection(
						RewardGroupIndex: 0,
						FixedRewardItems: [new QuestFinishRewardItem(ItemId: 182400001, Count: 2)],
						SelectableRewardItems: [new QuestFinishRewardItem(ItemId: 182400002, Count: 1)]),
				],
				ExtendedRewards: new QuestFinishRewardGroupProjection(
					RewardGroupIndex: -1,
					FixedRewardItems: [new QuestFinishRewardItem(ItemId: 186000001, Count: 5)])),
			DialogActionId: 8,
			RewardRepeatCount: 3);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			questState,
			new NearbyQuestTemplateSummary(1001),
			PlayerNpcFactionsSnapshot.Empty,
			now,
			CreateOptions("UTC"),
			rewardProjection);

		Assert.True(plan.Applied);
		Assert.Equal(0, plan.QuestState?.RewardGroup);
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardGroupCorrection,
			QuestFinishOperationAction.ItemRewardProjection,
			QuestFinishOperationAction.ItemRewardProjection,
			QuestFinishOperationAction.ItemRewardProjection,
			QuestFinishOperationAction.ItemRewardPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
			QuestFinishOperationAction.QuestUpdatePacket,
		], plan.Descriptors.Take(7).Select(descriptor => descriptor.Action));
		var projectedItems = plan.Descriptors
			.Where(descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardProjection)
			.ToArray();
		Assert.Equal(
		[
			QuestFinishRewardItemSource.ExtendedFixed,
			QuestFinishRewardItemSource.RegularFixed,
			QuestFinishRewardItemSource.RegularSelectable,
		], projectedItems.Select(descriptor => descriptor.RewardItemProjection!.Source));
		Assert.Equal([186000001, 182400001, 182400002], projectedItems.Select(descriptor => descriptor.ItemId));
		Assert.DoesNotContain(plan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardProjectionWarning);
	}

	[Fact]
	public void CreatePlan_ComposesRewardProjectionWarningsBeforeCoarsePlaceholder()
	{
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 1,
			HasItemRewards: true,
			ItemProjection: new QuestFinishRewardItemTemplateProjection(
				RewardGroups:
				[
					new QuestFinishRewardGroupProjection(RewardGroupIndex: 0),
				],
				HasBonus: true),
			DialogActionId: 0,
			RewardRepeatCount: 1);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			rewardProjection);

		var warningDescriptor = Assert.Single(
			plan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardProjectionWarning);
		Assert.NotNull(warningDescriptor.RewardItemProjectionWarning);
		Assert.Equal(
			QuestFinishRewardItemProjectionWarning.BonusHandlerNotProjected,
			warningDescriptor.RewardItemProjectionWarning.Warning);
		var descriptorList = plan.Descriptors.ToList();
		var warningIndex = descriptorList.IndexOf(warningDescriptor);
		var coarsePlaceholderIndex = descriptorList.FindIndex(
			descriptor => descriptor.Action == QuestFinishOperationAction.ItemRewardPlaceholder);
		Assert.True(warningIndex < coarsePlaceholderIndex);
	}

	[Fact]
	public void CreatePlan_ComposesDetailedNonItemRewardProjectionBeforeCoarsePlaceholder()
	{
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 1,
			HasNonItemRewards: true,
			NonItemProjection: new QuestFinishRewardNonItemTemplateProjection(
				Kinah: 1_000,
				Experience: 2_000,
				Title: 10,
				AbyssPoints: 30,
				DivinePoints: 40,
				GloryPoints: 50,
				ExtendInventory: 1),
			TargetNpcId: 203001,
			HasTargetNpcTemplate: true);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001, QuestCategory: "QUEST"),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			rewardProjection);

		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardGroupCorrection,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
		], plan.Descriptors.Take(10).Select(descriptor => descriptor.Action));
		var projectedRewards = plan.Descriptors
			.Where(descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardProjection)
			.ToArray();
		Assert.Equal(
		[
			QuestFinishRewardNonItemAction.Kinah,
			QuestFinishRewardNonItemAction.Experience,
			QuestFinishRewardNonItemAction.Title,
			QuestFinishRewardNonItemAction.AbyssPoints,
			QuestFinishRewardNonItemAction.DivinePoints,
			QuestFinishRewardNonItemAction.GloryPoints,
			QuestFinishRewardNonItemAction.CubeExpansion,
		], projectedRewards.Select(descriptor => descriptor.RewardNonItemProjection!.Action));
		Assert.Equal("Rates.XP_QUEST", projectedRewards[1].RewardNonItemProjection!.RateSource);
		Assert.Equal(203001, projectedRewards[1].RewardNonItemProjection!.TargetNpcId);
		Assert.Equal("Rates.AP_QUEST", projectedRewards[3].RewardNonItemProjection!.RateSource);
	}

	[Fact]
	public void CreatePlan_ComposesTitleAndCubeSideEffectPlansAfterMatchingNonItemProjection()
	{
		var player = new Player
		{
			ObjectId = 4100,
			Race = "ELYOS",
			QuestExpands = 1,
			NpcExpands = 2,
			ItemExpands = 3,
		};
		var titleTemplates = new TitleTemplateTable(
		[
			new TitleTemplateSummary(5, 412994, "quest title", "ELYOS", Array.Empty<ItemStatModifier>()),
		]);
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 1,
			HasNonItemRewards: true,
			NonItemProjection: new QuestFinishRewardNonItemTemplateProjection(
				Title: 5,
				ExtendInventory: 1));

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001, QuestCategory: "QUEST"),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			rewardProjection,
			rewardSideEffectContext: new QuestFinishRewardSideEffectContext(player, titleTemplates));

		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardGroupCorrection,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardSideEffectPlan,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardSideEffectPlan,
			QuestFinishOperationAction.NonItemRewardPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
		], plan.Descriptors.Take(7).Select(descriptor => descriptor.Action));
		var sideEffects = plan.Descriptors
			.Where(descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardSideEffectPlan)
			.ToArray();
		Assert.Equal(2, sideEffects.Length);
		Assert.Equal(QuestFinishRewardNonItemAction.Title, sideEffects[0].RewardNonItemProjection?.Action);
		Assert.Equal(QuestTitleRewardStatus.Applied, sideEffects[0].TitleRewardPlan?.Status);
		Assert.Equal(new PlayerTitle(5, 0), sideEffects[0].TitleRewardPlan?.Title);
		Assert.Equal(
		[
			QuestRewardPacketIntent.QuestTitleSystemMessage,
			QuestRewardPacketIntent.FullTitleInfo,
		], sideEffects[0].TitleRewardPlan?.PacketIntents);
		Assert.Equal(QuestFinishRewardNonItemAction.CubeExpansion, sideEffects[1].RewardNonItemProjection?.Action);
		Assert.Equal(QuestExpansionRewardKind.Cube, sideEffects[1].ExpansionRewardPlan?.Kind);
		Assert.Equal(QuestExpansionRewardStatus.Applied, sideEffects[1].ExpansionRewardPlan?.Status);
		Assert.Equal(90, sideEffects[1].ExpansionRewardPlan?.NewSlotLimit);
		Assert.Empty(player.Titles);
		Assert.Equal(1, player.QuestExpands);
	}

	[Fact]
	public void CreatePlan_ComposesGpSideEffectPlanAfterMatchingNonItemProjectionWithoutMutatingPlayer()
	{
		var player = new Player
		{
			ObjectId = 4102,
			AccountMembership = 1,
			AbyssRank = PlayerAbyssRank.Default() with { Gp = 100 },
		};
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 1,
			HasNonItemRewards: true,
			NonItemProjection: new QuestFinishRewardNonItemTemplateProjection(GloryPoints: 50));
		var options = new GameServerOptions
		{
			Core = new GameServerCoreOptions
			{
				TimeZoneId = "UTC",
			},
			Rates = new GameServerRateOptions
			{
				GpRates = [1f, 2f],
			},
		};

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001, QuestCategory: "QUEST"),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			options,
			rewardProjection,
			rewardSideEffectContext: new QuestFinishRewardSideEffectContext(player));

		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardGroupCorrection,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardSideEffectPlan,
			QuestFinishOperationAction.NonItemRewardPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
		], plan.Descriptors.Take(5).Select(descriptor => descriptor.Action));
		var sideEffect = Assert.Single(
			plan.Descriptors,
			descriptor => descriptor.GpRewardPlan is not null);
		Assert.Equal(QuestFinishRewardNonItemAction.GloryPoints, sideEffect.RewardNonItemProjection?.Action);
		Assert.Equal(QuestGpRewardStatus.Applied, sideEffect.GpRewardPlan?.Status);
		Assert.Equal(50, sideEffect.GpRewardPlan?.RewardGp);
		Assert.Equal(100, sideEffect.GpRewardPlan?.AppliedRewardGp);
		Assert.Equal(100, sideEffect.GpRewardPlan?.PreviousGp);
		Assert.Equal(200, sideEffect.GpRewardPlan?.CurrentGp);
		Assert.Equal(100, sideEffect.GpRewardPlan?.GloryPointsPlan?.Added);
		Assert.False(sideEffect.GpRewardPlan?.GloryPointsPlan?.RequiresOfflineDaoUpdate);
		Assert.Equal(100, player.AbyssRank.Gp);
		Assert.Equal(0, player.AbyssRank.DailyGp);
		Assert.Equal(0, player.AbyssRank.WeeklyGp);
	}

	[Fact]
	public void CreatePlan_ComposesXpSideEffectPlanAfterMatchingNonItemProjectionWithoutMutatingPlayer()
	{
		var player = new Player
		{
			ObjectId = 4103,
			AccountMembership = 1,
			Level = 15,
			Exp = 14_000,
			ReposeEnergy = 50,
		};
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 1,
			HasNonItemRewards: true,
			NonItemProjection: new QuestFinishRewardNonItemTemplateProjection(Experience: 100));
		var options = new GameServerOptions
		{
			Core = new GameServerCoreOptions
			{
				TimeZoneId = "UTC",
			},
			Rates = new GameServerRateOptions
			{
				XpQuestRates = [1f, 2f],
			},
		};

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001, QuestCategory: "QUEST"),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			options,
			rewardProjection,
			rewardSideEffectContext: new QuestFinishRewardSideEffectContext(
				player,
				ExperienceTable: CreateLinearExperienceTable(),
				TargetNpcName: "quest npc",
				SalvationPercent: 10));

		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardGroupCorrection,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardSideEffectPlan,
			QuestFinishOperationAction.NonItemRewardPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
		], plan.Descriptors.Take(5).Select(descriptor => descriptor.Action));
		var sideEffect = Assert.Single(
			plan.Descriptors,
			descriptor => descriptor.XpRewardPlan is not null);
		Assert.Equal(QuestFinishRewardNonItemAction.Experience, sideEffect.RewardNonItemProjection?.Action);
		Assert.Equal(QuestXpRewardStatus.Applied, sideEffect.XpRewardPlan?.Status);
		Assert.Equal(100, sideEffect.XpRewardPlan?.RewardXp);
		Assert.Equal(200, sideEffect.XpRewardPlan?.AppliedBaseXp);
		Assert.Equal(20, sideEffect.XpRewardPlan?.ReposeBonus);
		Assert.Equal(20, sideEffect.XpRewardPlan?.SalvationBonus);
		Assert.Equal(240, sideEffect.XpRewardPlan?.FinalRewardXp);
		Assert.Equal(14_240, sideEffect.XpRewardPlan?.CurrentExp);
		Assert.Equal(QuestXpRewardMessageKind.NamedReposeAndSalvationBonus, sideEffect.XpRewardPlan?.MessageKind);
		Assert.Equal(
		[
			QuestXpRewardPacketIntent.StatUpdateExp,
			QuestXpRewardPacketIntent.XpSystemMessage,
		], sideEffect.XpRewardPlan?.PacketIntents);
		Assert.Equal(14_000, player.Exp);
		Assert.Equal(15, player.Level);
		Assert.Equal(50, player.ReposeEnergy);
	}

	[Fact]
	public void CreatePlan_ComposesXpExecutionPlanWithLevelChangeContextMetadata()
	{
		var player = new Player
		{
			ObjectId = 4104,
			AccountId = 3301,
			Name = "Xpcontext",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 9,
			Exp = 8_900,
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 1,
			HasNonItemRewards: true,
			NonItemProjection: new QuestFinishRewardNonItemTemplateProjection(Experience: 400));
		var contextInput = new QuestXpLevelChangeContextFactoryInput(
			FromLevel: 10,
			ToLevel: 10,
			MaxStats: new PlayerLevelChangeUpgradeStats(250, 230, 120),
			QuestLevelChangedRegistrations: [new QuestLevelChangedRegistration(1001, "ELYOS")],
			GuidesEnabled: true,
			IsSpawned: true,
			GuideHtmlTemplates: [new GuideHtmlTemplateSummary("level 10", 10, "RANGER", "ELYOS")],
			IsDaeva: true,
			HasEffectController: true,
			BonusPackStoreReceivingPlayerSucceeded: true,
			FactionPackAccountCreationLocalTime: new DateTime(2020, 9, 14, 0, 0, 0),
			FactionPackStoreReceivingPlayerSucceeded: true,
			StarterKitEnabled: true);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001, QuestCategory: "QUEST"),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			rewardProjection,
			rewardSideEffectContext: new QuestFinishRewardSideEffectContext(
				player,
				ExperienceTable: CreateLinearExperienceTable(),
				LevelChangeContextInput: contextInput));

		var sideEffect = Assert.Single(
			plan.Descriptors,
			descriptor => descriptor.XpExecutionPlan is not null);
		Assert.NotNull(sideEffect.XpRewardPlan);
		Assert.Equal(QuestXpExecutionPlanStatus.Applied, sideEffect.XpExecutionPlan?.Status);
		Assert.True(sideEffect.XpExecutionPlan?.LevelChanged);
		Assert.Equal(
		[
			QuestXpExecutionAction.UpgradePlayerLifeStats,
			QuestXpExecutionAction.NpcFactionLevelUp,
			QuestXpExecutionAction.QuestLevelChangedCallbacks,
			QuestXpExecutionAction.NearbyQuestRefresh,
			QuestXpExecutionAction.GuideHtml,
			QuestXpExecutionAction.SkillAutoLearn,
			QuestXpExecutionAction.BonusPackReward,
			QuestXpExecutionAction.FactionPackReward,
			QuestXpExecutionAction.StarterKitReward,
		], sideEffect.XpExecutionPlan!.LevelChangeSubPlans.Select(subPlan => subPlan.Action));
		Assert.All(sideEffect.XpExecutionPlan.LevelChangeSubPlans, subPlan => Assert.False(subPlan.IsLive));
		Assert.Equal(8_900, player.Exp);
		Assert.Equal(9, player.Level);
	}

	[Fact]
	public void CreatePlan_ComposesWarehouseSideEffectPlanAndKeepsBoundaryFailuresNonLive()
	{
		var player = new Player
		{
			ObjectId = 4101,
			WarehouseNpcExpands = 7,
			WarehouseBonusExpands = 4,
		};
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 1,
			HasNonItemRewards: true,
			NonItemProjection: new QuestFinishRewardNonItemTemplateProjection(ExtendInventory: 2));

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001, QuestCategory: "QUEST"),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			rewardProjection,
			rewardSideEffectContext: new QuestFinishRewardSideEffectContext(player));

		var descriptorList = plan.Descriptors.ToList();
		var warehouseProjectionIndex = descriptorList.FindIndex(
			descriptor => descriptor.RewardNonItemProjection?.Action == QuestFinishRewardNonItemAction.WarehouseExpansion);
		var sideEffectDescriptor = Assert.Single(
			descriptorList,
			descriptor => descriptor.ExpansionRewardPlan?.Kind == QuestExpansionRewardKind.Warehouse);
		var sideEffectIndex = descriptorList.IndexOf(sideEffectDescriptor);
		var coarsePlaceholderIndex = descriptorList.FindIndex(
			descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardPlaceholder);
		Assert.True(warehouseProjectionIndex >= 0);
		Assert.True(warehouseProjectionIndex < sideEffectIndex);
		Assert.True(sideEffectIndex < coarsePlaceholderIndex);
		Assert.False(sideEffectDescriptor.IsLive);
		Assert.Equal(QuestExpansionRewardStatus.CannotExpand, sideEffectDescriptor.ExpansionRewardPlan?.Status);
		Assert.Equal([QuestRewardPacketIntent.CannotExpandSystemMessage], sideEffectDescriptor.ExpansionRewardPlan?.PacketIntents);
		Assert.Equal(4, player.WarehouseBonusExpands);
	}

	[Fact]
	public void CreatePlan_ComposesNonCountApRateBypassAndNonItemWarnings()
	{
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 1,
			HasNonItemRewards: true,
			NonItemProjection: new QuestFinishRewardNonItemTemplateProjection(
				AbyssPoints: 500,
				ExtendInventory: 3,
				ExtendStigma: 1));

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001, QuestCategory: "NON_COUNT"),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			rewardProjection);

		var apDescriptor = Assert.Single(
			plan.Descriptors,
			descriptor => descriptor.RewardNonItemProjection?.Action == QuestFinishRewardNonItemAction.AbyssPoints);
		Assert.True(apDescriptor.RewardNonItemProjection!.RateBypassed);
		Assert.Null(apDescriptor.RewardNonItemProjection.RateSource);
		Assert.Equal(500, apDescriptor.Count);
		Assert.Equal(
		[
			QuestFinishRewardNonItemProjectionWarning.UnsupportedExtendInventoryValue,
			QuestFinishRewardNonItemProjectionWarning.XmlFieldIgnoredByJavaGiveReward,
		], plan.Descriptors
			.Where(descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardProjectionWarning)
			.Select(descriptor => descriptor.RewardNonItemProjectionWarning!.Warning));
		var descriptorList = plan.Descriptors.ToList();
		var lastWarningIndex = descriptorList.FindLastIndex(
			descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardProjectionWarning);
		var coarsePlaceholderIndex = descriptorList.FindIndex(
			descriptor => descriptor.Action == QuestFinishOperationAction.NonItemRewardPlaceholder);
		Assert.True(lastWarningIndex < coarsePlaceholderIndex);
	}

	[Fact]
	public void CreatePlan_ComposesDetailedPersistencePlansAfterNearbyRefresh()
	{
		var now = new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero);
		var questState = new PlayerQuestState(
			QuestId: 35007,
			Status: "REWARD",
			QuestVars: 4,
			Flags: 0,
			CompleteCount: 0);
		var npcFactions = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Start,
				QuestId: 35007),
		]);
		var questPersistencePlan = QuestPersistencePlanService.CreatePlan(
		[
			new QuestPersistenceStateEntry(questState, QuestPersistenceState.UpdateRequired),
		],
		[
			777,
		]);
		var npcFactionPersistencePlan = NpcFactionPersistencePlanService.CreatePlan(
		[
			new NpcFactionPersistenceStateEntry(
				new PlayerNpcFactionState(
					FactionId: 2,
					IsActive: true,
					IsMentor: false,
					TimeEpochSeconds: 1_779_800_400,
					State: PlayerNpcFactionQuestState.Complete,
					QuestId: 35007),
				NpcFactionPersistenceState.UpdateRequired),
			new NpcFactionPersistenceStateEntry(
				new PlayerNpcFactionState(
					FactionId: 8,
					IsActive: false,
					IsMentor: true,
					TimeEpochSeconds: 0,
					State: PlayerNpcFactionQuestState.Noting,
					QuestId: 0),
				NpcFactionPersistenceState.New),
		]);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			questState,
			new NearbyQuestTemplateSummary(35007, NpcFactionId: 2),
			npcFactions,
			now,
			CreateOptions("UTC"),
			questPersistencePlan: questPersistencePlan,
			npcFactionPersistencePlan: npcFactionPersistencePlan);

		Assert.True(plan.Applied);
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardMutationPlaceholder,
			QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
			QuestFinishOperationAction.QuestUpdatePacket,
			QuestFinishOperationAction.QuestCompletedCallback,
			QuestFinishOperationAction.NpcFactionCompletion,
			QuestFinishOperationAction.NearbyQuestRefresh,
			QuestFinishOperationAction.DeferredQuestPersistence,
			QuestFinishOperationAction.DeferredQuestPersistence,
			QuestFinishOperationAction.DeferredNpcFactionPersistence,
			QuestFinishOperationAction.DeferredNpcFactionPersistence,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal(Enumerable.Range(1, 11), plan.Descriptors.Select(descriptor => descriptor.Order));
		var persistenceDescriptors = plan.Descriptors
			.SkipWhile(descriptor => descriptor.Action != QuestFinishOperationAction.NearbyQuestRefresh)
			.Skip(1)
			.ToArray();
		Assert.Equal(
		[
			QuestPersistenceOperationAction.Delete,
			QuestPersistenceOperationAction.Update,
		], persistenceDescriptors
			.Where(descriptor => descriptor.QuestPersistenceOperation is not null)
			.Select(descriptor => descriptor.QuestPersistenceOperation!.Action));
		Assert.Equal(
		[
			NpcFactionPersistenceOperationAction.Update,
			NpcFactionPersistenceOperationAction.Insert,
		], persistenceDescriptors
			.Where(descriptor => descriptor.NpcFactionPersistenceOperation is not null)
			.Select(descriptor => descriptor.NpcFactionPersistenceOperation!.Action));
		Assert.All(
			persistenceDescriptors,
			descriptor => Assert.False(descriptor.IsLive));
	}

	[Fact]
	public void CreatePlan_PreservesJavaFailureOrderingAcrossRewardsCallbacksAndDeferredPersistence()
	{
		var now = new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero);
		var questState = new PlayerQuestState(
			QuestId: 35007,
			Status: "REWARD",
			QuestVars: 4,
			Flags: 0,
			CompleteCount: 0);
		var npcFactions = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Start,
				QuestId: 35007),
		]);
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			RewardGroupCount: 1,
			HasItemRewards: true,
			HasNonItemRewards: true,
			IsChallengeTask: true,
			ItemProjection: new QuestFinishRewardItemTemplateProjection(
				RewardGroups:
				[
					new QuestFinishRewardGroupProjection(
						RewardGroupIndex: 0,
						FixedRewardItems: [new QuestFinishRewardItem(ItemId: 182400001, Count: 2)]),
				]),
			NonItemProjection: new QuestFinishRewardNonItemTemplateProjection(Kinah: 500),
			WorkItems:
			[
				new QuestFinishRewardWorkItem(ItemId: 182400002, Count: 1),
			]);
		var callbackPlan = QuestCompletionCallbackPlanService.CreatePlan(
			35007,
		[
			new QuestCompletionCallbackRegistration(
				RegisteredQuestId: 14015,
				HandlerJavaSource: "game-server/data/handlers/quest/verteron/_14015NotBlindedByVengeance.java"),
		]);
		var questPersistencePlan = QuestPersistencePlanService.CreatePlan(
		[
			new QuestPersistenceStateEntry(questState, QuestPersistenceState.UpdateRequired),
		],
			deletedQuestIds: []);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			questState,
			new NearbyQuestTemplateSummary(35007, NpcFactionId: 2),
			npcFactions,
			now,
			CreateOptions("UTC"),
			rewardProjection,
			callbackPlan,
			questPersistencePlan);

		Assert.True(plan.Applied);
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardGroupCorrection,
			QuestFinishOperationAction.ItemRewardProjection,
			QuestFinishOperationAction.ItemRewardPlaceholder,
			QuestFinishOperationAction.NonItemRewardProjection,
			QuestFinishOperationAction.NonItemRewardPlaceholder,
			QuestFinishOperationAction.ChallengeTaskCompletionPlaceholder,
			QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
			QuestFinishOperationAction.QuestUpdatePacket,
			QuestFinishOperationAction.QuestCompletedCallback,
			QuestFinishOperationAction.NpcFactionCompletion,
			QuestFinishOperationAction.NearbyQuestRefresh,
			QuestFinishOperationAction.DeferredQuestPersistence,
			QuestFinishOperationAction.DeferredNpcFactionPersistence,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal(Enumerable.Range(1, 14), plan.Descriptors.Select(descriptor => descriptor.Order));

		var actions = plan.Descriptors.Select(descriptor => descriptor.Action).ToList();
		Assert.True(actions.IndexOf(QuestFinishOperationAction.NonItemRewardPlaceholder) < actions.IndexOf(QuestFinishOperationAction.QuestStateMutation));
		Assert.True(actions.IndexOf(QuestFinishOperationAction.QuestStateMutation) < actions.IndexOf(QuestFinishOperationAction.QuestUpdatePacket));
		Assert.True(actions.IndexOf(QuestFinishOperationAction.QuestUpdatePacket) < actions.IndexOf(QuestFinishOperationAction.QuestCompletedCallback));
		Assert.True(actions.IndexOf(QuestFinishOperationAction.QuestCompletedCallback) < actions.IndexOf(QuestFinishOperationAction.NpcFactionCompletion));
		Assert.True(actions.IndexOf(QuestFinishOperationAction.NpcFactionCompletion) < actions.IndexOf(QuestFinishOperationAction.NearbyQuestRefresh));
		Assert.True(actions.IndexOf(QuestFinishOperationAction.NearbyQuestRefresh) < actions.IndexOf(QuestFinishOperationAction.DeferredQuestPersistence));
		Assert.True(actions.IndexOf(QuestFinishOperationAction.NearbyQuestRefresh) < actions.IndexOf(QuestFinishOperationAction.DeferredNpcFactionPersistence));
	}

	[Fact]
	public void CreatePlan_ComposesDetailedCallbackPlanAfterUpdatePacketBeforeNpcFaction()
	{
		var now = new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero);
		var npcFactions = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Start,
				QuestId: 35007),
		]);
		var callbackPlan = QuestCompletionCallbackPlanService.CreatePlan(
			35007,
		[
			new QuestCompletionCallbackRegistration(
				RegisteredQuestId: 14015,
				HandlerJavaSource: "game-server/data/handlers/quest/verteron/_14015NotBlindedByVengeance.java",
				UsesDefaultFollowUp: true,
				FollowUpQuestId: 14015),
			new QuestCompletionCallbackRegistration(
				RegisteredQuestId: 1002,
				HandlerJavaSource: "game-server/data/handlers/quest/poeta/_1002RequestoftheElim.java"),
		]);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(35007, "REWARD", QuestVars: 4, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(35007, NpcFactionId: 2),
			npcFactions,
			now,
			CreateOptions("UTC"),
			callbackPlan: callbackPlan);

		Assert.True(plan.Applied);
		Assert.Equal(
		[
			QuestFinishOperationAction.RewardMutationPlaceholder,
			QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder,
			QuestFinishOperationAction.QuestStateMutation,
			QuestFinishOperationAction.QuestUpdatePacket,
			QuestFinishOperationAction.QuestCompletedCallback,
			QuestFinishOperationAction.QuestCompletedCallback,
			QuestFinishOperationAction.NpcFactionCompletion,
			QuestFinishOperationAction.NearbyQuestRefresh,
			QuestFinishOperationAction.DeferredQuestPersistence,
			QuestFinishOperationAction.DeferredNpcFactionPersistence,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal(Enumerable.Range(1, 10), plan.Descriptors.Select(descriptor => descriptor.Order));
		var callbackDescriptors = plan.Descriptors
			.Where(descriptor => descriptor.Action == QuestFinishOperationAction.QuestCompletedCallback)
			.ToArray();
		Assert.Equal([14015, 1002], callbackDescriptors.Select(descriptor => descriptor.CompletionCallbackOperation?.RegisteredQuestId));
		Assert.All(callbackDescriptors, descriptor => Assert.NotNull(descriptor.CompletionCallbackOperation));
		Assert.All(callbackDescriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.True(callbackDescriptors[0].CompletionCallbackOperation!.UsesDefaultFollowUp);
		Assert.Equal(14015, callbackDescriptors[0].CompletionCallbackOperation!.FollowUpQuestId);
		Assert.Equal(35007, callbackDescriptors[0].CompletionCallbackOperation!.CompletedQuestId);
	}

	[Fact]
	public void CreatePlan_PreservesNestedCallbackFollowUpPlanThroughQuestFinishOrdering()
	{
		var followUpPlan = QuestCompletionFollowUpPlanService.CreatePlan(
		[
			new QuestCompletionFollowUpRequest(
				FollowUpQuestId: 14015,
				Decision: QuestCompletionFollowUpDecision.Lock,
				StartConditionsEvaluatedByCaller: true),
			new QuestCompletionFollowUpRequest(
				FollowUpQuestId: 14016,
				Decision: QuestCompletionFollowUpDecision.Start,
				ExistingQuestState: new PlayerQuestState(14016, "LOCKED", QuestVars: 0, Flags: 0, CompleteCount: 0)),
		]);
		var callbackPlan = QuestCompletionCallbackPlanService.CreatePlan(
			35007,
		[
			new QuestCompletionCallbackRegistration(
				RegisteredQuestId: 14015,
				HandlerJavaSource: "game-server/data/handlers/quest/verteron/_14015NotBlindedByVengeance.java",
				UsesDefaultFollowUp: true,
				FollowUpQuestId: 14015,
				FollowUpPlan: followUpPlan),
		]);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(35007, "REWARD", QuestVars: 4, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(35007, NpcFactionId: 0),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			callbackPlan: callbackPlan);

		var callbackDescriptor = Assert.Single(
			plan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.QuestCompletedCallback);
		Assert.NotNull(callbackDescriptor.CompletionCallbackOperation);
		Assert.Same(followUpPlan, callbackDescriptor.CompletionCallbackOperation!.FollowUpPlan);
		Assert.Equal(
		[
			QuestFinishOperationAction.QuestUpdatePacket,
			QuestFinishOperationAction.QuestCompletedCallback,
			QuestFinishOperationAction.NearbyQuestRefresh,
		], plan.Descriptors
			.Where(descriptor => descriptor.Action is
				QuestFinishOperationAction.QuestUpdatePacket or
				QuestFinishOperationAction.QuestCompletedCallback or
				QuestFinishOperationAction.NearbyQuestRefresh)
			.Select(descriptor => descriptor.Action));
		Assert.Equal(
		[
			QuestCompletionFollowUpPacketAction.Add,
			QuestCompletionFollowUpPacketAction.Update,
		], callbackDescriptor.CompletionCallbackOperation.FollowUpPlan!.Descriptors.Select(descriptor => descriptor.PacketAction));
		Assert.Equal(["LOCKED", "START"], callbackDescriptor.CompletionCallbackOperation.FollowUpPlan.Descriptors.Select(descriptor => descriptor.TargetQuestStatus));
		Assert.All(callbackDescriptor.CompletionCallbackOperation.FollowUpPlan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
	}

	[Fact]
	public void CreatePlan_UsesProvidedEmptyCallbackPlanWithoutLegacyPlaceholder()
	{
		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 1, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			callbackPlan: new QuestCompletionCallbackPlan(
				QuestCompletionCallbackPlanStatus.NoHandlers,
				Array.Empty<QuestCompletionCallbackDescriptor>()));

		Assert.DoesNotContain(plan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.QuestCompletedCallback);
		Assert.Equal(
		[
			QuestFinishOperationAction.QuestStateMutation,
			QuestFinishOperationAction.QuestUpdatePacket,
			QuestFinishOperationAction.NearbyQuestRefresh,
		], plan.Descriptors
			.Where(descriptor => descriptor.Action is
				QuestFinishOperationAction.QuestStateMutation or
				QuestFinishOperationAction.QuestUpdatePacket or
				QuestFinishOperationAction.NearbyQuestRefresh)
			.Select(descriptor => descriptor.Action));
	}

	[Fact]
	public void CreatePlan_UsesProvidedEmptyPersistencePlansWithoutLegacyPlaceholders()
	{
		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(1001, "REWARD", QuestVars: 1, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(1001),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			questPersistencePlan: new QuestPersistencePlan(
				QuestPersistencePlanStatus.NoChanges,
				Array.Empty<QuestPersistenceOperationDescriptor>()),
			npcFactionPersistencePlan: new NpcFactionPersistencePlan(
				NpcFactionPersistencePlanStatus.NoChanges,
				Array.Empty<NpcFactionPersistenceOperationDescriptor>()));

		Assert.DoesNotContain(plan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.DeferredQuestPersistence);
		Assert.DoesNotContain(plan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.DeferredNpcFactionPersistence);
		Assert.Equal(QuestFinishOperationAction.NearbyQuestRefresh, plan.Descriptors.Last().Action);
	}

	[Fact]
	public void CreatePlan_ComposesOptionalBonusInputAssemblyDescriptorWithoutInvokingAdapter()
	{
		var rewardProjection = new QuestFinishRewardTemplateProjection(
			HasItemRewards: true,
			ItemProjection: new QuestFinishRewardItemTemplateProjection(
				HasBonus: true,
				BonusProjection: new QuestFinishRewardBonusTemplateProjection(
					"MOVIE",
					0,
					QuestFinishRewardBonusSupportStatus.SilentNoOpInJavaBonusService)));
		var bonusAssemblyPlan = QuestFinishBonusRewardInputAssemblyPlanService.CreatePlan(
			new QuestFinishBonusRewardInputAssemblyRequest(
				rewardProjection,
				new NearbyQuestTemplateSummary(80016),
				new PlayerQuestState(80016, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
				new Player { Race = "ELYOS" }));

		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(80016, "REWARD", QuestVars: 0, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(80016),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			rewardProjection,
			bonusRewardInputAssemblyPlan: bonusAssemblyPlan);

		var descriptor = Assert.Single(
			plan.Descriptors,
			descriptor => descriptor.Action == QuestFinishOperationAction.BonusRewardInputAssembly);
		Assert.Equal(1, descriptor.Order);
		Assert.False(descriptor.IsLive);
		Assert.Same(bonusAssemblyPlan, descriptor.BonusRewardInputAssemblyPlan);
		Assert.True(descriptor.BonusRewardInputAssemblyPlan!.CreatedInput);
		Assert.True(descriptor.Order < plan.Descriptors.Single(item => item.Action == QuestFinishOperationAction.ItemRewardPlaceholder).Order);
		Assert.Contains(plan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.QuestStateMutation);
	}

	[Fact]
	public void CreatePlan_KeepsNpcFactionNoOpDescriptorWhenJavaWouldReturnFromMissingActiveSlot()
	{
		var plan = QuestFinishOperationPlanService.CreatePlan(
			new PlayerQuestState(35007, "REWARD", QuestVars: 4, Flags: 0, CompleteCount: 0),
			new NearbyQuestTemplateSummary(35007, NpcFactionId: 2, IsMentorQuest: true),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.True(plan.Applied);
		Assert.Contains(plan.Descriptors, descriptor => descriptor.Action == QuestFinishOperationAction.NpcFactionCompletion);
		Assert.Empty(plan.NpcFactions.Factions);
	}

	[Theory]
	[InlineData("START", QuestFinishStateMutationStatus.NotRewardState)]
	[InlineData("COMPLETE", QuestFinishStateMutationStatus.NotRewardState)]
	public void CreatePlan_ReturnsNoDescriptorsWhenQuestFinishGuardFails(
		string status,
		QuestFinishStateMutationStatus expectedStatus)
	{
		var questState = new PlayerQuestState(1001, status, QuestVars: 1, Flags: 0, CompleteCount: 0);

		var plan = QuestFinishOperationPlanService.CreatePlan(
			questState,
			new NearbyQuestTemplateSummary(1001),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"),
			new QuestFinishRewardTemplateProjection(RewardGroupCount: 2, HasItemRewards: true));

		Assert.False(plan.Applied);
		Assert.Equal(expectedStatus, plan.Status);
		Assert.Empty(plan.Descriptors);
		Assert.Same(questState, plan.QuestState);
	}

	[Fact]
	public void CreatePlan_ReturnsNoDescriptorsWhenQuestStateIsMissing()
	{
		var plan = QuestFinishOperationPlanService.CreatePlan(
			null,
			new NearbyQuestTemplateSummary(1001),
			PlayerNpcFactionsSnapshot.Empty,
			new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero),
			CreateOptions("UTC"));

		Assert.False(plan.Applied);
		Assert.Equal(QuestFinishStateMutationStatus.MissingQuestState, plan.Status);
		Assert.Null(plan.QuestState);
		Assert.Empty(plan.Descriptors);
	}

	private static GameServerOptions CreateOptions(string timeZoneId)
	{
		return new GameServerOptions
		{
			Core = new GameServerCoreOptions
			{
				TimeZoneId = timeZoneId,
			},
		};
	}

	private static PlayerExperienceTable CreateLinearExperienceTable()
	{
		return new PlayerExperienceTable(Enumerable.Range(0, 70).Select(level => (long)level * 1000).ToArray());
	}
}
