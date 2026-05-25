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
			CreateOptions("UTC"));

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
