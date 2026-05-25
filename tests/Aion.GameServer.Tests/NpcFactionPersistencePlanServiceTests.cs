using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcFactionPersistencePlanServiceTests
{
	[Fact]
	public void CreatePlan_EmitsInsertAndUpdateOperationsLikeJavaNpcFactionDao()
	{
		var plan = NpcFactionPersistencePlanService.CreatePlan(
		[
			Entry(12, NpcFactionPersistenceState.Updated),
			Entry(2, NpcFactionPersistenceState.New),
			Entry(8, NpcFactionPersistenceState.UpdateRequired),
			Entry(13, NpcFactionPersistenceState.Deleted),
			Entry(14, NpcFactionPersistenceState.NoAction),
		]);

		Assert.Equal(NpcFactionPersistencePlanStatus.Ready, plan.Status);
		Assert.True(plan.HasOperations);
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Equal(
		[
			NpcFactionPersistenceOperationAction.Insert,
			NpcFactionPersistenceOperationAction.Update,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.Equal([2, 8], plan.Descriptors.Select(descriptor => descriptor.FactionId));
		Assert.Equal([1, 2], plan.Descriptors.Select(descriptor => descriptor.Order));
	}

	[Fact]
	public void CreatePlan_PreservesFactionPayloadForInsertAndUpdate()
	{
		var insertState = new PlayerNpcFactionState(
			FactionId: 2,
			IsActive: true,
			IsMentor: false,
			TimeEpochSeconds: 1_779_800_400,
			State: PlayerNpcFactionQuestState.Noting,
			QuestId: 14001);
		var updateState = new PlayerNpcFactionState(
			FactionId: 8,
			IsActive: false,
			IsMentor: true,
			TimeEpochSeconds: 1_779_886_800,
			State: PlayerNpcFactionQuestState.Complete,
			QuestId: 24001);

		var plan = NpcFactionPersistencePlanService.CreatePlan(
		[
			new NpcFactionPersistenceStateEntry(updateState, NpcFactionPersistenceState.UpdateRequired),
			new NpcFactionPersistenceStateEntry(insertState, NpcFactionPersistenceState.New),
		]);

		var update = Assert.Single(plan.Descriptors, descriptor => descriptor.Action == NpcFactionPersistenceOperationAction.Update);
		var insert = Assert.Single(plan.Descriptors, descriptor => descriptor.Action == NpcFactionPersistenceOperationAction.Insert);
		Assert.Same(updateState, update.FactionState);
		Assert.False(update.FactionState.IsActive);
		Assert.True(update.FactionState.IsMentor);
		Assert.Equal(1_779_886_800, update.FactionState.TimeEpochSeconds);
		Assert.Equal(PlayerNpcFactionQuestState.Complete, update.FactionState.State);
		Assert.Equal(24001, update.FactionState.QuestId);
		Assert.Same(insertState, insert.FactionState);
		Assert.True(insert.FactionState.IsActive);
		Assert.False(insert.FactionState.IsMentor);
		Assert.Equal(1_779_800_400, insert.FactionState.TimeEpochSeconds);
		Assert.Equal(PlayerNpcFactionQuestState.Noting, insert.FactionState.State);
		Assert.Equal(14001, insert.FactionState.QuestId);
	}

	[Fact]
	public void CreatePlan_PreservesCallerOrderBecauseJavaUsesHashMapValues()
	{
		var plan = NpcFactionPersistencePlanService.CreatePlan(
		[
			Entry(9, NpcFactionPersistenceState.UpdateRequired),
			Entry(2, NpcFactionPersistenceState.New),
			Entry(12, NpcFactionPersistenceState.UpdateRequired),
			Entry(8, NpcFactionPersistenceState.New),
		]);

		Assert.Equal([9, 2, 12, 8], plan.Descriptors.Select(descriptor => descriptor.FactionId));
	}

	[Fact]
	public void CreatePlan_ReturnsNoChangesForIgnoredJavaPersistentStates()
	{
		var plan = NpcFactionPersistencePlanService.CreatePlan(
		[
			Entry(2, NpcFactionPersistenceState.Updated),
			Entry(8, NpcFactionPersistenceState.Deleted),
			Entry(12, NpcFactionPersistenceState.NoAction),
		]);

		Assert.Equal(NpcFactionPersistencePlanStatus.NoChanges, plan.Status);
		Assert.False(plan.HasOperations);
		Assert.Empty(plan.Descriptors);
	}

	private static NpcFactionPersistenceStateEntry Entry(
		int factionId,
		NpcFactionPersistenceState persistenceState)
	{
		return new NpcFactionPersistenceStateEntry(
			new PlayerNpcFactionState(
				FactionId: factionId,
				IsActive: true,
				IsMentor: factionId == 8,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Start,
				QuestId: factionId * 100),
			persistenceState);
	}
}
