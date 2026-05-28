using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TargetSelectResolutionPlanServiceTests
{
	[Fact]
	public void CreatePlan_ClearsTargetWhenClientRequestsZeroLikeJava()
	{
		var plan = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 0,
			SelectTargetOfTarget: false));

		Assert.Equal(TargetSelectResolutionStatus.ClearedTarget, plan.Status);
		Assert.True(plan.ShouldCallSetTarget);
		Assert.Equal(0, plan.ResolvedTargetObjectId);
		Assert.Equal(TargetSelectSystemMessage.None, plan.SystemMessage);
	}

	[Fact]
	public void CreatePlan_SelectsSelfBeforeKnownListLookupLikeJava()
	{
		var plan = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 1001,
			SelectTargetOfTarget: false));

		Assert.Equal(TargetSelectResolutionStatus.SelectedSelf, plan.Status);
		Assert.True(plan.ShouldCallSetTarget);
		Assert.Equal(1001, plan.ResolvedTargetObjectId);
	}

	[Fact]
	public void CreatePlan_SelectsKnownVisibleObjectLikeJava()
	{
		var plan = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 7002,
			SelectTargetOfTarget: false,
			KnownTargetObjectId: 7002,
			KnownTargetSeenByPlayer: true));

		Assert.Equal(TargetSelectResolutionStatus.SelectedKnownObject, plan.Status);
		Assert.True(plan.ShouldCallSetTarget);
		Assert.Equal(7002, plan.ResolvedTargetObjectId);
		Assert.Null(plan.AuditMessage);
	}

	[Fact]
	public void CreatePlan_AuditsAndClearsInvisibleKnownObjectLikeJava()
	{
		var plan = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 7002,
			SelectTargetOfTarget: false,
			KnownTargetObjectId: 7002,
			KnownTargetSeenByPlayer: false));

		Assert.Equal(TargetSelectResolutionStatus.InvisibleKnownTargetAuditedAndCleared, plan.Status);
		Assert.True(plan.ShouldCallSetTarget);
		Assert.Equal(0, plan.ResolvedTargetObjectId);
		Assert.Contains("radar hack", plan.AuditMessage);
	}

	[Fact]
	public void CreatePlan_SelectsTeamMemberFallbackWhenKnownListMissesLikeJava()
	{
		var plan = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 2002,
			SelectTargetOfTarget: false,
			TeamMemberObjectId: 2002));

		Assert.Equal(TargetSelectResolutionStatus.SelectedTeamMember, plan.Status);
		Assert.True(plan.ShouldCallSetTarget);
		Assert.Equal(2002, plan.ResolvedTargetObjectId);
	}

	[Fact]
	public void CreatePlan_ReturnsAssistMessageWhenSelectingTargetOfTargetWithoutCurrentTargetLikeJava()
	{
		var plan = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 0,
			SelectTargetOfTarget: true,
			CurrentTargetObjectId: 0));

		Assert.Equal(TargetSelectResolutionStatus.AssistNoCurrentTarget, plan.Status);
		Assert.False(plan.ShouldCallSetTarget);
		Assert.Equal(TargetSelectSystemMessage.AssistThisIsAssistKey, plan.SystemMessage);
		Assert.Equal(0, plan.ResolvedTargetObjectId);
	}

	[Fact]
	public void CreatePlan_ReturnsAssistNoUserWhenCurrentTargetHasNoTargetLikeJava()
	{
		var plan = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 0,
			SelectTargetOfTarget: true,
			CurrentTargetObjectId: 7002,
			TargetOfTargetObjectId: 0));

		Assert.Equal(TargetSelectResolutionStatus.AssistNoTargetOfTarget, plan.Status);
		Assert.False(plan.ShouldCallSetTarget);
		Assert.Equal(TargetSelectSystemMessage.AssistNoUser, plan.SystemMessage);
	}

	[Fact]
	public void CreatePlan_SelectsVisibleTargetOfTargetLikeJava()
	{
		var plan = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 0,
			SelectTargetOfTarget: true,
			CurrentTargetObjectId: 7002,
			TargetOfTargetObjectId: 7003,
			TargetOfTargetKnownByPlayer: true,
			TargetOfTargetSeenByPlayer: true));

		Assert.Equal(TargetSelectResolutionStatus.SelectedTargetOfTarget, plan.Status);
		Assert.True(plan.ShouldCallSetTarget);
		Assert.Equal(7003, plan.ResolvedTargetObjectId);
		Assert.Equal(TargetSelectSystemMessage.None, plan.SystemMessage);
	}

	[Fact]
	public void CreatePlan_ReturnsAssistTooFarWhenTargetOfTargetIsUnknownAndUnseenLikeJava()
	{
		var plan = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 0,
			SelectTargetOfTarget: true,
			CurrentTargetObjectId: 7002,
			TargetOfTargetObjectId: 7003,
			TargetOfTargetKnownByPlayer: false,
			TargetOfTargetSeenByPlayer: false));

		Assert.Equal(TargetSelectResolutionStatus.AssistTargetTooFar, plan.Status);
		Assert.False(plan.ShouldCallSetTarget);
		Assert.Equal(TargetSelectSystemMessage.AssistTooFar, plan.SystemMessage);
	}

	[Fact]
	public void CreatePlan_ReturnsAssistNoUserWhenTargetOfTargetIsKnownButNotVisibleLikeJava()
	{
		var plan = TargetSelectResolutionPlanService.CreatePlan(new TargetSelectResolutionInput(
			PlayerObjectId: 1001,
			RequestedTargetObjectId: 0,
			SelectTargetOfTarget: true,
			CurrentTargetObjectId: 7002,
			TargetOfTargetObjectId: 7003,
			TargetOfTargetKnownByPlayer: true,
			TargetOfTargetSeenByPlayer: false));

		Assert.Equal(TargetSelectResolutionStatus.AssistTargetNotVisible, plan.Status);
		Assert.False(plan.ShouldCallSetTarget);
		Assert.Equal(TargetSelectSystemMessage.AssistNoUser, plan.SystemMessage);
		Assert.Equal(0, plan.ResolvedTargetObjectId);
	}
}
