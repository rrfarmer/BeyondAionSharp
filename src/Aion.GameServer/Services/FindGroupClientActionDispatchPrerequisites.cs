namespace Aion.GameServer.Services;

public static class FindGroupClientActionDispatchPrerequisites
{
	public static FindGroupClientActionDispatchPrerequisitePlan Inspect(FindGroupClientAction action)
	{
		// Java parity: network/aion/clientpackets/CM_FIND_GROUP.runImpl resolves these facts
		// from the connection, FindGroupService singleton, World, config/data managers, and
		// PacketSendUtility. This is a readiness map only; it enables no live dispatch.
		var requirements = new List<FindGroupClientActionRuntimeRequirement>
		{
			FindGroupClientActionRuntimeRequirement.ActivePlayer,
			FindGroupClientActionRuntimeRequirement.FindGroupStateStore,
		};

		switch (action.Action)
		{
			case 0:
			case 4:
			case 10:
			case 13:
				requirements.Add(FindGroupClientActionRuntimeRequirement.CurrentEpochSeconds);
				requirements.Add(FindGroupClientActionRuntimeRequirement.DirectPacketDispatch);
				break;
			case 1:
			case 5:
				requirements.Add(FindGroupClientActionRuntimeRequirement.WorldBroadcastDispatch);
				break;
			case 2:
				requirements.Add(FindGroupClientActionRuntimeRequirement.CurrentEpochSeconds);
				requirements.Add(FindGroupClientActionRuntimeRequirement.CurrentTeamSnapshot);
				requirements.Add(FindGroupClientActionRuntimeRequirement.DirectPacketDispatch);
				break;
			case 3:
			case 7:
			case 17:
				requirements.Add(FindGroupClientActionRuntimeRequirement.CurrentEpochSeconds);
				break;
			case 6:
				requirements.Add(FindGroupClientActionRuntimeRequirement.CurrentEpochSeconds);
				requirements.Add(FindGroupClientActionRuntimeRequirement.DirectPacketDispatch);
				break;
			case 8:
				requirements.Add(FindGroupClientActionRuntimeRequirement.CurrentEpochSeconds);
				requirements.Add(FindGroupClientActionRuntimeRequirement.CurrentInstanceGroupMembers);
				requirements.Add(FindGroupClientActionRuntimeRequirement.DirectPacketDispatch);
				break;
			case 9:
				requirements.Add(FindGroupClientActionRuntimeRequirement.CurrentEpochSeconds);
				requirements.Add(FindGroupClientActionRuntimeRequirement.DirectPacketDispatch);
				break;
			case 11:
				requirements.Add(FindGroupClientActionRuntimeRequirement.WorldPlayerLookup);
				requirements.Add(FindGroupClientActionRuntimeRequirement.DirectPacketDispatch);
				break;
			case 12:
				requirements.Add(FindGroupClientActionRuntimeRequirement.WorldPlayerLookup);
				requirements.Add(FindGroupClientActionRuntimeRequirement.DirectPacketDispatch);
				requirements.Add(FindGroupClientActionRuntimeRequirement.GroupAllianceInviteDispatch);
				break;
			case 15:
				requirements.Add(FindGroupClientActionRuntimeRequirement.CurrentEpochSeconds);
				requirements.Add(FindGroupClientActionRuntimeRequirement.CurrentInstanceGroupMembers);
				requirements.Add(FindGroupClientActionRuntimeRequirement.DirectPacketDispatch);
				break;
			case 20:
			case 25:
				return new FindGroupClientActionDispatchPrerequisitePlan(
					action.Action,
					FindGroupClientActionDispatchReadiness.ParsedButNoJavaRunImpl,
					[],
					"CM_FIND_GROUP.readImpl parses this action, but runImpl has no branch.");
			default:
				return new FindGroupClientActionDispatchPrerequisitePlan(
					action.Action,
					FindGroupClientActionDispatchReadiness.UnknownAction,
					[],
					"CM_FIND_GROUP.runImpl has no branch for this action.");
		}

		if (action.Action == 10)
		{
			requirements.Add(FindGroupClientActionRuntimeRequirement.GroupConfigFormInstanceGroupAnywhere);
			requirements.Add(FindGroupClientActionRuntimeRequirement.TargetNpcSnapshot);
			requirements.Add(FindGroupClientActionRuntimeRequirement.AutoGroupDataLookup);
		}

		return new FindGroupClientActionDispatchPrerequisitePlan(
			action.Action,
			FindGroupClientActionDispatchReadiness.DeferredUntilRuntimeFactsAreAvailable,
			requirements.Distinct().ToArray(),
			"Live CM_FIND_GROUP dispatch remains deferred until all Java-equivalent runtime facts and side-effect dispatchers are sourced.");
	}
}

public enum FindGroupClientActionDispatchReadiness
{
	DeferredUntilRuntimeFactsAreAvailable,
	ParsedButNoJavaRunImpl,
	UnknownAction,
}

public enum FindGroupClientActionRuntimeRequirement
{
	ActivePlayer,
	FindGroupStateStore,
	CurrentEpochSeconds,
	CurrentTeamSnapshot,
	CurrentInstanceGroupMembers,
	WorldPlayerLookup,
	DirectPacketDispatch,
	WorldBroadcastDispatch,
	GroupAllianceInviteDispatch,
	GroupConfigFormInstanceGroupAnywhere,
	TargetNpcSnapshot,
	AutoGroupDataLookup,
}

public sealed record FindGroupClientActionDispatchPrerequisitePlan(
	int Action,
	FindGroupClientActionDispatchReadiness Readiness,
	IReadOnlyList<FindGroupClientActionRuntimeRequirement> Requirements,
	string JavaSource);
