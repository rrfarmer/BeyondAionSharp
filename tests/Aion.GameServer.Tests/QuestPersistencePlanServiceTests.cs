using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestPersistencePlanServiceTests
{
	[Fact]
	public void CreatePlan_EmitsDeleteInsertUpdatePhasesLikeJavaQuestDao()
	{
		var plan = QuestPersistencePlanService.CreatePlan(
		[
			Entry(3003, QuestPersistenceState.UpdateRequired),
			Entry(1001, QuestPersistenceState.New),
			Entry(2002, QuestPersistenceState.Deleted),
			Entry(4004, QuestPersistenceState.Updated),
			Entry(5005, QuestPersistenceState.NoAction),
		],
		[
			7007,
		]);

		Assert.Equal(QuestPersistencePlanStatus.Ready, plan.Status);
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			QuestPersistenceOperationAction.Delete,
			QuestPersistenceOperationAction.Delete,
			QuestPersistenceOperationAction.Insert,
			QuestPersistenceOperationAction.Update,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal([2002, 7007, 1001, 3003], plan.Descriptors.Select(descriptor => descriptor.QuestId));
		Assert.Equal([1, 2, 3, 4], plan.Descriptors.Select(descriptor => descriptor.Order));
		Assert.True(plan.Descriptors[1].FromDeletedQuestIdSet);
		Assert.Null(plan.Descriptors[1].QuestState);
	}

	[Fact]
	public void CreatePlan_CarriesNullableRewardAndTimeFieldsForInsertAndUpdate()
	{
		var nextRepeat = new DateTimeOffset(2026, 5, 26, 9, 0, 0, TimeSpan.Zero);
		var completeTime = new DateTimeOffset(2026, 5, 25, 8, 30, 0, TimeSpan.Zero);
		var insertState = new PlayerQuestState(
			QuestId: 1001,
			Status: "COMPLETE",
			QuestVars: 0,
			Flags: 2,
			CompleteCount: 3,
			RewardGroup: null,
			NextRepeatTime: nextRepeat,
			CompleteTime: completeTime);
		var updateState = insertState with
		{
			QuestId = 1002,
			RewardGroup = 1,
			NextRepeatTime = null,
			CompleteTime = null,
		};

		var plan = QuestPersistencePlanService.CreatePlan(
		[
			new QuestPersistenceStateEntry(updateState, QuestPersistenceState.UpdateRequired),
			new QuestPersistenceStateEntry(insertState, QuestPersistenceState.New),
		]);

		var insert = Assert.Single(plan.Descriptors, descriptor => descriptor.Action == QuestPersistenceOperationAction.Insert);
		var update = Assert.Single(plan.Descriptors, descriptor => descriptor.Action == QuestPersistenceOperationAction.Update);
		Assert.Same(insertState, insert.QuestState);
		Assert.NotNull(insert.QuestState);
		Assert.Null(insert.QuestState!.RewardGroup);
		Assert.Equal(nextRepeat, insert.QuestState.NextRepeatTime);
		Assert.Equal(completeTime, insert.QuestState.CompleteTime);
		Assert.Same(updateState, update.QuestState);
		Assert.NotNull(update.QuestState);
		Assert.Equal(1, update.QuestState!.RewardGroup);
		Assert.Null(update.QuestState.NextRepeatTime);
		Assert.Null(update.QuestState.CompleteTime);
	}

	[Fact]
	public void CreatePlan_ReturnsNoChangesForUpdatedNoActionAndNoDeletedIds()
	{
		var plan = QuestPersistencePlanService.CreatePlan(
		[
			Entry(1001, QuestPersistenceState.Updated),
			Entry(1002, QuestPersistenceState.NoAction),
		]);

		Assert.Equal(QuestPersistencePlanStatus.NoChanges, plan.Status);
		Assert.False(plan.HasOperations);
		Assert.Empty(plan.Descriptors);
	}

	[Fact]
	public void CreatePlan_OrdersCurrentQuestRowsByQuestIdLikeJavaTreeMap()
	{
		var plan = QuestPersistencePlanService.CreatePlan(
		[
			Entry(3003, QuestPersistenceState.New),
			Entry(1001, QuestPersistenceState.New),
			Entry(5005, QuestPersistenceState.UpdateRequired),
			Entry(4004, QuestPersistenceState.UpdateRequired),
			Entry(2002, QuestPersistenceState.Deleted),
		]);

		Assert.Equal([2002, 1001, 3003, 4004, 5005], plan.Descriptors.Select(descriptor => descriptor.QuestId));
	}

	private static QuestPersistenceStateEntry Entry(int questId, QuestPersistenceState persistenceState)
	{
		return new QuestPersistenceStateEntry(
			new PlayerQuestState(
				QuestId: questId,
				Status: "START",
				QuestVars: questId,
				Flags: 0,
				CompleteCount: 0),
			persistenceState);
	}
}
