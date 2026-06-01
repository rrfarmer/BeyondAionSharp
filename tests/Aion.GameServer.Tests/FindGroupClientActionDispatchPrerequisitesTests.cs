using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupClientActionDispatchPrerequisitesTests
{
	[Fact]
	public void Inspect_ActionTenRequiresMaskListRuntimeSourcesBeforeLiveDispatch()
	{
		var plan = FindGroupClientActionDispatchPrerequisites.Inspect(new FindGroupClientAction(10));

		Assert.Equal(10, plan.Action);
		Assert.Equal(FindGroupClientActionDispatchReadiness.DeferredUntilRuntimeFactsAreAvailable, plan.Readiness);
		AssertRequirements(
			plan,
			FindGroupClientActionRuntimeRequirement.ActivePlayer,
			FindGroupClientActionRuntimeRequirement.FindGroupStateStore,
			FindGroupClientActionRuntimeRequirement.CurrentEpochSeconds,
			FindGroupClientActionRuntimeRequirement.DirectPacketDispatch,
			FindGroupClientActionRuntimeRequirement.GroupConfigFormInstanceGroupAnywhere,
			FindGroupClientActionRuntimeRequirement.TargetNpcSnapshot,
			FindGroupClientActionRuntimeRequirement.AutoGroupDataLookup);
	}

	[Theory]
	[InlineData(11, FindGroupClientActionRuntimeRequirement.DirectPacketDispatch)]
	[InlineData(12, FindGroupClientActionRuntimeRequirement.GroupAllianceInviteDispatch)]
	public void Inspect_InstanceApplicationActionsRequireWorldPlayerLookup(int action, FindGroupClientActionRuntimeRequirement sideEffectRequirement)
	{
		var plan = FindGroupClientActionDispatchPrerequisites.Inspect(new FindGroupClientAction(action));

		Assert.Equal(FindGroupClientActionDispatchReadiness.DeferredUntilRuntimeFactsAreAvailable, plan.Readiness);
		Assert.Contains(FindGroupClientActionRuntimeRequirement.WorldPlayerLookup, plan.Requirements);
		Assert.Contains(sideEffectRequirement, plan.Requirements);
	}

	[Theory]
	[InlineData(20)]
	[InlineData(25)]
	public void Inspect_ParsedActionsWithoutJavaRunImplHaveNoLiveRequirements(int action)
	{
		var plan = FindGroupClientActionDispatchPrerequisites.Inspect(new FindGroupClientAction(action));

		Assert.Equal(action, plan.Action);
		Assert.Equal(FindGroupClientActionDispatchReadiness.ParsedButNoJavaRunImpl, plan.Readiness);
		Assert.Empty(plan.Requirements);
		Assert.Equal("CM_FIND_GROUP.readImpl parses this action, but runImpl has no branch.", plan.JavaSource);
	}

	[Fact]
	public void Inspect_UnknownActionHasNoRuntimeRequirements()
	{
		var plan = FindGroupClientActionDispatchPrerequisites.Inspect(new FindGroupClientAction(99));

		Assert.Equal(FindGroupClientActionDispatchReadiness.UnknownAction, plan.Readiness);
		Assert.Empty(plan.Requirements);
	}

	private static void AssertRequirements(
		FindGroupClientActionDispatchPrerequisitePlan plan,
		params FindGroupClientActionRuntimeRequirement[] expected)
	{
		Assert.Equal(expected.Order().ToArray(), plan.Requirements.Order().ToArray());
	}
}
