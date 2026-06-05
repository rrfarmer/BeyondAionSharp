using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CompleteAscensionQuestPlanServiceTests
{
	[Fact]
	public void CreatePlan_ElyosQuestIdIs1006()
	{
		var plan = CompleteAscensionQuestPlanService.CreatePlan("ELYOS", questStateExists: false);

		Assert.Equal(1006, plan.QuestId);
	}

	[Fact]
	public void CreatePlan_AsmodiansQuestIdIs2008()
	{
		var plan = CompleteAscensionQuestPlanService.CreatePlan("ASMODIANS", questStateExists: false);

		Assert.Equal(2008, plan.QuestId);
	}

	[Fact]
	public void CreatePlan_NewQuestStateProducesAddThenUpdate()
	{
		var plan = CompleteAscensionQuestPlanService.CreatePlan("ELYOS", questStateExists: false);

		Assert.Equal(CompleteAscensionQuestPlanStatus.PlanCreatedWithAdd, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendAddFirst);
		Assert.NotNull(plan.AddPacket);
		Assert.NotNull(plan.UpdatePacket);
		Assert.Contains("ADD", plan.JavaSource);
		Assert.Contains("UPDATE", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_ExistingQuestStateProducesUpdateOnly()
	{
		var plan = CompleteAscensionQuestPlanService.CreatePlan("ELYOS", questStateExists: true);

		Assert.Equal(CompleteAscensionQuestPlanStatus.PlanCreatedWithUpdateOnly, plan.Status);
		Assert.False(plan.ShouldSendAddFirst);
		Assert.Null(plan.AddPacket);
		Assert.NotNull(plan.UpdatePacket);
	}

	[Fact]
	public void CreatePlan_FinalQuestStateIsCompleteWithZeroVarAndRewardGroup()
	{
		var plan = CompleteAscensionQuestPlanService.CreatePlan("ELYOS", questStateExists: false);

		Assert.Equal("COMPLETE", plan.FinalQuestState.Status);
		Assert.Equal(0, plan.FinalQuestState.QuestVars);
		Assert.Null(plan.FinalQuestState.RewardGroup); // setRewardGroup(0) is live/deferred; C# null means unset
	}

	[Fact]
	public void SmQuestActionConstants_MatchJavaActionTypeIds()
	{
		// Java: SM_QUEST_ACTION.ActionType ids.
		Assert.Equal(1, Aion.GameServer.Network.Aion.ServerPackets.SmQuestAction.AddActionId);
		Assert.Equal(2, Aion.GameServer.Network.Aion.ServerPackets.SmQuestAction.UpdateActionId);
		Assert.Equal(3, Aion.GameServer.Network.Aion.ServerPackets.SmQuestAction.AbandonActionId);
		Assert.Equal(4, Aion.GameServer.Network.Aion.ServerPackets.SmQuestAction.TimerActionId);
		Assert.Equal(5, Aion.GameServer.Network.Aion.ServerPackets.SmQuestAction.ShareActionId);
		Assert.Equal(6, Aion.GameServer.Network.Aion.ServerPackets.SmQuestAction.UnknownActionId);
	}

	[Fact]
	public void CreatePlan_IsCaseInsensitive()
	{
		var plan = CompleteAscensionQuestPlanService.CreatePlan("elyos", questStateExists: false);

		Assert.Equal(1006, plan.QuestId);
	}
}
