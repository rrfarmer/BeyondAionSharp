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
}
