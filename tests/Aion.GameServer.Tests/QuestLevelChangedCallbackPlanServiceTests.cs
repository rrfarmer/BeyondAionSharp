using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestLevelChangedCallbackPlanServiceTests
{
	[Fact]
	public void CreatePlan_DispatchesRegisteredRaceCallbacksInJavaOrderAndSkipsCompleteAndMissingHandlers()
	{
		var registrations = new[]
		{
			new QuestLevelChangedRegistration(1001, RacePermitted: null),
			new QuestLevelChangedRegistration(1002, RacePermitted: "ELYOS"),
			new QuestLevelChangedRegistration(2001, RacePermitted: "ASMODIANS"),
			new QuestLevelChangedRegistration(1003, RacePermitted: "ELYOS", HasHandler: false),
			new QuestLevelChangedRegistration(1002, RacePermitted: "ELYOS"),
		};
		var questStates = new[]
		{
			QuestState(1002, "COMPLETE"),
			QuestState(1003, "LOCKED"),
		};

		var plan = QuestLevelChangedCallbackPlanService.CreatePlan("ELYOS", registrations, questStates);

		Assert.True(plan.Applied);
		Assert.Equal(QuestLevelChangedCallbackPlanStatus.Applied, plan.Status);
		Assert.Equal(
		[
			1001,
			1002,
			2001,
			1003,
		], plan.Descriptors.Select(descriptor => descriptor.QuestId));
		Assert.Equal(
		[
			QuestLevelChangedCallbackDescriptorStatus.PlannedDispatch,
			QuestLevelChangedCallbackDescriptorStatus.SkippedComplete,
			QuestLevelChangedCallbackDescriptorStatus.SkippedRace,
			QuestLevelChangedCallbackDescriptorStatus.SkippedMissingHandler,
		], plan.Descriptors.Select(descriptor => descriptor.Status));
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.Null(plan.Descriptors[0].QuestState);
		Assert.Equal("COMPLETE", plan.Descriptors[1].QuestState!.Status);
		Assert.Equal("LOCKED", plan.Descriptors[3].QuestState!.Status);
	}

	[Fact]
	public void CreatePlan_TreatsEveryNonCompleteQuestStateAsDispatchableLikeJava()
	{
		var registrations = new[]
		{
			new QuestLevelChangedRegistration(1001, RacePermitted: null),
			new QuestLevelChangedRegistration(1002, RacePermitted: null),
			new QuestLevelChangedRegistration(1003, RacePermitted: null),
			new QuestLevelChangedRegistration(1004, RacePermitted: null),
		};
		var questStates = new[]
		{
			QuestState(1001, "START"),
			QuestState(1002, "REWARD"),
			QuestState(1003, "LOCKED"),
			QuestState(1004, "COMPLETE"),
		};

		var plan = QuestLevelChangedCallbackPlanService.CreatePlan("ASMODIAN", registrations, questStates);

		Assert.Equal(QuestLevelChangedCallbackPlanStatus.Applied, plan.Status);
		Assert.Equal(
		[
			QuestLevelChangedCallbackDescriptorStatus.PlannedDispatch,
			QuestLevelChangedCallbackDescriptorStatus.PlannedDispatch,
			QuestLevelChangedCallbackDescriptorStatus.PlannedDispatch,
			QuestLevelChangedCallbackDescriptorStatus.SkippedComplete,
		], plan.Descriptors.Select(descriptor => descriptor.Status));
	}

	[Fact]
	public void CreatePlan_RecordsNoRegisteredNoDispatchAndMissingRegistrationBranches()
	{
		var noRegistered = QuestLevelChangedCallbackPlanService.CreatePlan("ELYOS", Array.Empty<QuestLevelChangedRegistration>(), Array.Empty<PlayerQuestState>());
		var noDispatches = QuestLevelChangedCallbackPlanService.CreatePlan(
			"ELYOS",
			[new QuestLevelChangedRegistration(2001, RacePermitted: "ASMODIANS")],
			Array.Empty<PlayerQuestState>());
		var missing = QuestLevelChangedCallbackPlanService.CreatePlan("ELYOS", registrations: null, questStates: null);

		Assert.Equal(QuestLevelChangedCallbackPlanStatus.NoRegisteredCallbacks, noRegistered.Status);
		Assert.Empty(noRegistered.Descriptors);
		Assert.Equal(QuestLevelChangedCallbackPlanStatus.NoDispatches, noDispatches.Status);
		Assert.Equal(QuestLevelChangedCallbackDescriptorStatus.SkippedRace, Assert.Single(noDispatches.Descriptors).Status);
		Assert.Equal(QuestLevelChangedCallbackPlanStatus.MissingRegistrations, missing.Status);
		Assert.Empty(missing.Descriptors);
	}

	private static PlayerQuestState QuestState(int questId, string status)
	{
		return new PlayerQuestState(
			questId,
			status,
			QuestVars: 0,
			Flags: 0,
			CompleteCount: string.Equals(status, "COMPLETE", StringComparison.Ordinal) ? 1 : 0);
	}
}
