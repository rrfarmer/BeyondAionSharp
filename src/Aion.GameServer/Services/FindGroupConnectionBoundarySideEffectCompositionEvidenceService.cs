namespace Aion.GameServer.Services;

public static class FindGroupConnectionBoundarySideEffectCompositionEvidenceService
{
	public static FindGroupConnectionBoundarySideEffectIntentPlan CreateIntentPlan(
		FindGroupConnectionClientActionCompositionPlan compositionPlan)
	{
		if (compositionPlan.ClientActionPlan == null)
		{
			return new FindGroupConnectionBoundarySideEffectIntentPlan(
				compositionPlan.Status,
				compositionPlan.Action.Action,
				ClientActionKind: null,
				DirectPacketIntents: [],
				WorldBroadcastIntents: [],
				InstanceGroupStatus: null,
				InstanceGroupMemberInfoStatus: null,
				InstanceApplicationStatus: null,
				InviteIntent: null,
				compositionPlan.ShouldDispatchLiveSideEffects,
				IsCmFindGroupBoundaryWired: false,
				"CM_FIND_GROUP composition skipped before a client action plan was available.");
		}

		var clientPlan = compositionPlan.ClientActionPlan;
		var directPacketIntents = CollectDirectPacketIntents(clientPlan, compositionPlan.ActivePlayer?.ObjectId);
		var worldBroadcastIntents = CollectWorldBroadcastIntents(clientPlan);

		return new FindGroupConnectionBoundarySideEffectIntentPlan(
			compositionPlan.Status,
			compositionPlan.Action.Action,
			clientPlan.Kind,
			directPacketIntents,
			worldBroadcastIntents,
			clientPlan.InstanceGroupMutationPlan?.Status,
			clientPlan.InstanceGroupMemberInfoPlan?.Status,
			clientPlan.InstanceApplicationPlan?.Status,
			clientPlan.InstanceApplicationPlan?.InviteIntent,
			compositionPlan.ShouldDispatchLiveSideEffects || clientPlan.DispatchLiveSideEffects,
			IsCmFindGroupBoundaryWired: false,
			"Disabled CM_FIND_GROUP boundary composition only; opt-in executor evidence must be invoked explicitly.");
	}

	public static async Task<FindGroupConnectionBoundarySideEffectExecutionEvidence> ExecuteOptInAsync(
		FindGroupConnectionClientActionCompositionPlan compositionPlan,
		FindGroupSideEffectDispatchExecutorService executor)
	{
		var intentPlan = CreateIntentPlan(compositionPlan);
		var executionPlan = await executor.ExecuteAsync(
			intentPlan.DirectPacketIntents,
			intentPlan.WorldBroadcastIntents);

		return new FindGroupConnectionBoundarySideEffectExecutionEvidence(
			intentPlan,
			executionPlan,
			IsCmFindGroupBoundaryWired: false,
			"Executor was invoked by controlled evidence code, not by GameServerConnection.CmFindGroup.");
	}

	private static IReadOnlyList<FindGroupDirectPacketIntent> CollectDirectPacketIntents(
		FindGroupClientActionPlan clientPlan,
		int? activePlayerObjectId)
	{
		var intents = new List<FindGroupDirectPacketIntent>();
		AddRange(intents, clientPlan.RecruitmentMutationPlan?.DirectPacketIntents);
		AddShowRecruitmentsIntent(intents, activePlayerObjectId, clientPlan.RecruitmentMutationPlan?.ShowRecruitmentsPlan);
		AddShowRecruitmentsIntent(intents, activePlayerObjectId, clientPlan.RecruitmentShowPlan);
		AddRange(intents, clientPlan.ApplicationMutationPlan?.DirectPacketIntents);
		AddShowApplicationsIntent(intents, activePlayerObjectId, clientPlan.ApplicationMutationPlan?.ShowApplicationsPlan);
		AddShowApplicationsIntent(intents, activePlayerObjectId, clientPlan.ApplicationShowPlan);
		AddRange(intents, clientPlan.InstanceGroupMutationPlan?.DirectPacketIntents);
		AddShowInstanceGroupsIntent(intents, activePlayerObjectId, clientPlan.InstanceGroupMutationPlan?.ShowInstanceGroupsPlan);
		if (clientPlan.InstanceGroupClientShowPlan?.EnableRegisterForInstancesIntent != null)
			intents.Add(clientPlan.InstanceGroupClientShowPlan.EnableRegisterForInstancesIntent);
		AddShowInstanceGroupsIntent(intents, activePlayerObjectId, clientPlan.InstanceGroupClientShowPlan?.ShowInstanceGroupsPlan);
		if (clientPlan.InstanceGroupClientShowPlan == null)
			AddShowInstanceGroupsIntent(intents, activePlayerObjectId, clientPlan.InstanceGroupShowPlan);
		AddRange(intents, clientPlan.InstanceGroupMemberInfoPlan?.DirectPacketIntents);
		AddRange(intents, clientPlan.InstanceApplicationPlan?.DirectPacketIntents);
		return intents;
	}

	private static void AddShowRecruitmentsIntent(
		ICollection<FindGroupDirectPacketIntent> intents,
		int? activePlayerObjectId,
		FindGroupRecruitmentShowPlan? plan)
	{
		if (activePlayerObjectId == null || plan == null)
			return;

		intents.Add(new FindGroupDirectPacketIntent(
			activePlayerObjectId.Value,
			plan.Packet,
			"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(0, recruitments))"));
	}

	private static void AddShowApplicationsIntent(
		ICollection<FindGroupDirectPacketIntent> intents,
		int? activePlayerObjectId,
		FindGroupApplicationShowPlan? plan)
	{
		if (activePlayerObjectId == null || plan == null)
			return;

		intents.Add(new FindGroupDirectPacketIntent(
			activePlayerObjectId.Value,
			plan.Packet,
			"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(4, applications))"));
	}

	private static void AddShowInstanceGroupsIntent(
		ICollection<FindGroupDirectPacketIntent> intents,
		int? activePlayerObjectId,
		FindGroupInstanceGroupShowPlan? plan)
	{
		if (activePlayerObjectId == null || plan == null)
			return;

		intents.Add(new FindGroupDirectPacketIntent(
			activePlayerObjectId.Value,
			plan.Packet,
			"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))"));
	}

	private static IReadOnlyList<FindGroupWorldBroadcastIntent> CollectWorldBroadcastIntents(FindGroupClientActionPlan clientPlan)
	{
		var intents = new List<FindGroupWorldBroadcastIntent>();
		Add(intents, clientPlan.RecruitmentMutationPlan?.WorldBroadcastIntent);
		Add(intents, clientPlan.ApplicationMutationPlan?.WorldBroadcastIntent);
		return intents;
	}

	private static void AddRange<T>(ICollection<T> target, IEnumerable<T>? source)
	{
		if (source == null)
			return;

		foreach (var item in source)
			target.Add(item);
	}

	private static void Add<T>(ICollection<T> target, T? item)
		where T : class
	{
		if (item != null)
			target.Add(item);
	}
}

public sealed record FindGroupConnectionBoundarySideEffectIntentPlan(
	FindGroupConnectionClientActionCompositionStatus CompositionStatus,
	int Action,
	FindGroupClientActionPlanKind? ClientActionKind,
	IReadOnlyList<FindGroupDirectPacketIntent> DirectPacketIntents,
	IReadOnlyList<FindGroupWorldBroadcastIntent> WorldBroadcastIntents,
	FindGroupInstanceGroupPlanStatus? InstanceGroupStatus,
	FindGroupInstanceGroupPlanStatus? InstanceGroupMemberInfoStatus,
	FindGroupInstanceApplicationPlanStatus? InstanceApplicationStatus,
	FindGroupInstanceInviteIntent? InviteIntent,
	bool ShouldDispatchLiveSideEffects,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote);

public sealed record FindGroupConnectionBoundarySideEffectExecutionEvidence(
	FindGroupConnectionBoundarySideEffectIntentPlan IntentPlan,
	FindGroupSideEffectDispatchExecutionPlan ExecutionPlan,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote);
