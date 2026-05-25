using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestCompletionFollowUpPlanServiceTests
{
	[Fact]
	public void CreatePlan_AddsLockedFollowUpQuestWhenJavaWouldAddMissingCampaign()
	{
		var plan = QuestCompletionFollowUpPlanService.CreatePlan(
		[
			new QuestCompletionFollowUpRequest(
				FollowUpQuestId: 14015,
				Decision: QuestCompletionFollowUpDecision.Lock,
				StartConditionsEvaluatedByCaller: true),
		]);

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(QuestCompletionFollowUpPlanStatus.Ready, plan.Status);
		Assert.True(plan.HasOperations);
		Assert.Equal(1, descriptor.Order);
		Assert.Equal(14015, descriptor.FollowUpQuestId);
		Assert.Equal("LOCKED", descriptor.TargetQuestStatus);
		Assert.Equal(QuestCompletionFollowUpPacketAction.Add, descriptor.PacketAction);
		Assert.True(descriptor.StartConditionsEvaluatedByCaller);
		Assert.False(descriptor.IsLive);
		Assert.Null(descriptor.ExistingQuestState);
	}

	[Fact]
	public void CreatePlan_StartsMissingFollowUpQuestWithAddPacketLikeJavaAddOrUpdateQuest()
	{
		var plan = QuestCompletionFollowUpPlanService.CreatePlan(
		[
			new QuestCompletionFollowUpRequest(
				FollowUpQuestId: 1002,
				Decision: QuestCompletionFollowUpDecision.Start),
		]);

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal("START", descriptor.TargetQuestStatus);
		Assert.Equal(QuestCompletionFollowUpPacketAction.Add, descriptor.PacketAction);
	}

	[Fact]
	public void CreatePlan_UpdatesExistingNonCompleteFollowUpQuest()
	{
		var existing = new PlayerQuestState(1002, "LOCKED", QuestVars: 0, Flags: 0, CompleteCount: 0);

		var plan = QuestCompletionFollowUpPlanService.CreatePlan(
		[
			new QuestCompletionFollowUpRequest(
				FollowUpQuestId: 1002,
				Decision: QuestCompletionFollowUpDecision.Start,
				ExistingQuestState: existing),
		]);

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Same(existing, descriptor.ExistingQuestState);
		Assert.Equal("START", descriptor.TargetQuestStatus);
		Assert.Equal(QuestCompletionFollowUpPacketAction.Update, descriptor.PacketAction);
	}

	[Fact]
	public void CreatePlan_AddsExistingCompleteFollowUpQuestLikeJavaCompleteStatusBranch()
	{
		var existing = new PlayerQuestState(1002, "COMPLETE", QuestVars: 0, Flags: 0, CompleteCount: 1);

		var plan = QuestCompletionFollowUpPlanService.CreatePlan(
		[
			new QuestCompletionFollowUpRequest(
				FollowUpQuestId: 1002,
				Decision: QuestCompletionFollowUpDecision.Start,
				ExistingQuestState: existing),
		]);

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(QuestCompletionFollowUpPacketAction.Add, descriptor.PacketAction);
		Assert.Equal("START", descriptor.TargetQuestStatus);
	}

	[Fact]
	public void CreatePlan_ReturnsNoActionForSameStatusOrNoActionDecision()
	{
		var plan = QuestCompletionFollowUpPlanService.CreatePlan(
		[
			new QuestCompletionFollowUpRequest(
				FollowUpQuestId: 1002,
				Decision: QuestCompletionFollowUpDecision.Start,
				ExistingQuestState: new PlayerQuestState(1002, "START", QuestVars: 0, Flags: 0, CompleteCount: 0)),
			new QuestCompletionFollowUpRequest(
				FollowUpQuestId: 1003,
				Decision: QuestCompletionFollowUpDecision.NoAction),
		]);

		Assert.Equal(QuestCompletionFollowUpPlanStatus.NoAction, plan.Status);
		Assert.False(plan.HasOperations);
		Assert.Empty(plan.Descriptors);
	}
}
