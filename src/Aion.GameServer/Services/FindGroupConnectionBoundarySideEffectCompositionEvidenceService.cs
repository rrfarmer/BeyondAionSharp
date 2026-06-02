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
				InviteIntent: null,
				compositionPlan.ShouldDispatchLiveSideEffects,
				IsCmFindGroupBoundaryWired: false,
				"CM_FIND_GROUP composition skipped before a client action plan was available.");
		}

		var clientPlan = compositionPlan.ClientActionPlan;
		var directPacketIntents = CollectDirectPacketIntents(clientPlan);
		var worldBroadcastIntents = CollectWorldBroadcastIntents(clientPlan);

		return new FindGroupConnectionBoundarySideEffectIntentPlan(
			compositionPlan.Status,
			compositionPlan.Action.Action,
			clientPlan.Kind,
			directPacketIntents,
			worldBroadcastIntents,
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

	private static IReadOnlyList<FindGroupDirectPacketIntent> CollectDirectPacketIntents(FindGroupClientActionPlan clientPlan)
	{
		var intents = new List<FindGroupDirectPacketIntent>();
		AddRange(intents, clientPlan.RecruitmentMutationPlan?.DirectPacketIntents);
		AddRange(intents, clientPlan.ApplicationMutationPlan?.DirectPacketIntents);
		AddRange(intents, clientPlan.InstanceGroupMutationPlan?.DirectPacketIntents);
		if (clientPlan.InstanceGroupClientShowPlan?.EnableRegisterForInstancesIntent != null)
			intents.Add(clientPlan.InstanceGroupClientShowPlan.EnableRegisterForInstancesIntent);
		AddRange(intents, clientPlan.InstanceGroupMemberInfoPlan?.DirectPacketIntents);
		AddRange(intents, clientPlan.InstanceApplicationPlan?.DirectPacketIntents);
		return intents;
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
	FindGroupInstanceInviteIntent? InviteIntent,
	bool ShouldDispatchLiveSideEffects,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote);

public sealed record FindGroupConnectionBoundarySideEffectExecutionEvidence(
	FindGroupConnectionBoundarySideEffectIntentPlan IntentPlan,
	FindGroupSideEffectDispatchExecutionPlan ExecutionPlan,
	bool IsCmFindGroupBoundaryWired,
	string BoundaryNote);
