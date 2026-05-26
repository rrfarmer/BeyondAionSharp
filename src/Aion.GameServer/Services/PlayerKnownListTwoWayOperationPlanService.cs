namespace Aion.GameServer.Services;

public enum PlayerKnownListTwoWayOperationKind
{
	Add,
	Remove,
	Clear,
}

public enum PlayerKnownListTwoWayOperationStatus
{
	Planned,
	RejectedSelf,
	OwnerAwarenessRejected,
	CandidateAwarenessRejected,
	OwnerAlreadyKnowsCandidate,
	CandidateAlreadyKnowsOwner,
	NothingToRemove,
}

public enum PlayerKnownListTwoWayOperationStepKind
{
	CandidateAddsOwner,
	OwnerAddsCandidate,
	CandidateSeesOwner,
	OwnerSeesCandidate,
	OwnerRemovesCandidate,
	OwnerNotSeesCandidate,
	OwnerNotKnowsCandidate,
	CandidateRemovesOwner,
	CandidateNotSeesOwner,
	CandidateNotKnowsOwner,
}

public sealed record PlayerKnownListTwoWayOperationState(
	int OwnerPlayerObjectId,
	int CandidatePlayerObjectId,
	bool OwnerKnowsCandidate,
	bool CandidateKnowsOwner,
	bool OwnerAwareOfCandidate = true,
	bool CandidateAwareOfOwner = true,
	bool OwnerSeesCandidate = false,
	bool CandidateSeesOwner = false);

public sealed record PlayerKnownListTwoWayOperationStep(
	PlayerKnownListTwoWayOperationStepKind Kind,
	int OwnerPlayerObjectId,
	int CandidatePlayerObjectId,
	string JavaSource);

public sealed record PlayerKnownListTwoWayOperationPlan(
	PlayerKnownListTwoWayOperationKind Kind,
	PlayerKnownListTwoWayOperationStatus Status,
	int OwnerPlayerObjectId,
	int CandidatePlayerObjectId,
	IReadOnlyList<PlayerKnownListTwoWayOperationStep> Steps,
	bool MutatesLiveMembership,
	bool RequiresCandidateAddBeforeOwnerAdd,
	bool RequiresOwnerRemoveBeforeCandidateRemove,
	bool IsJavaRegionKnownListParity,
	string JavaSource,
	bool IsLive);

public sealed class PlayerKnownListTwoWayOperationPlanService
{
	public PlayerKnownListTwoWayOperationPlan PlanAdd(PlayerKnownListTwoWayOperationState state)
	{
		// Java parity breadcrumb: KnownList.findVisibleObjects checks owner awareness,
		// then calls newObject.getKnownList().add(owner) before owner.add(newObject).
		if (state.OwnerPlayerObjectId == state.CandidatePlayerObjectId)
			return CreatePlan(PlayerKnownListTwoWayOperationKind.Add, PlayerKnownListTwoWayOperationStatus.RejectedSelf, state, []);

		if (!state.OwnerAwareOfCandidate)
			return CreatePlan(PlayerKnownListTwoWayOperationKind.Add, PlayerKnownListTwoWayOperationStatus.OwnerAwarenessRejected, state, []);

		if (state.OwnerKnowsCandidate)
			return CreatePlan(PlayerKnownListTwoWayOperationKind.Add, PlayerKnownListTwoWayOperationStatus.OwnerAlreadyKnowsCandidate, state, []);

		if (!state.CandidateAwareOfOwner)
			return CreatePlan(PlayerKnownListTwoWayOperationKind.Add, PlayerKnownListTwoWayOperationStatus.CandidateAwarenessRejected, state, []);

		if (state.CandidateKnowsOwner)
			return CreatePlan(PlayerKnownListTwoWayOperationKind.Add, PlayerKnownListTwoWayOperationStatus.CandidateAlreadyKnowsOwner, state, []);

		return CreatePlan(
			PlayerKnownListTwoWayOperationKind.Add,
			PlayerKnownListTwoWayOperationStatus.Planned,
			state,
			new[]
			{
				new PlayerKnownListTwoWayOperationStep(
					PlayerKnownListTwoWayOperationStepKind.CandidateAddsOwner,
					state.OwnerPlayerObjectId,
					state.CandidatePlayerObjectId,
					"com.aionemu.gameserver.world.knownlist.KnownList.findVisibleObjects -> newObject.getKnownList().add(owner)"),
			}
			.Concat(CreateSeeStepIfVisible(
				state.CandidateSeesOwner,
				PlayerKnownListTwoWayOperationStepKind.CandidateSeesOwner,
				state,
				"com.aionemu.gameserver.world.knownlist.KnownList.updateVisibility -> notifySee(owner) after candidate add"))
			.Concat(
			new[]
			{
				new PlayerKnownListTwoWayOperationStep(
					PlayerKnownListTwoWayOperationStepKind.OwnerAddsCandidate,
					state.OwnerPlayerObjectId,
					state.CandidatePlayerObjectId,
					"com.aionemu.gameserver.world.knownlist.KnownList.findVisibleObjects -> add(newObject)"),
			})
			.Concat(CreateSeeStepIfVisible(
				state.OwnerSeesCandidate,
				PlayerKnownListTwoWayOperationStepKind.OwnerSeesCandidate,
				state,
				"com.aionemu.gameserver.world.knownlist.KnownList.updateVisibility -> notifySee(newObject) after owner add"))
			.ToArray());
	}

	public PlayerKnownListTwoWayOperationPlan PlanRemove(PlayerKnownListTwoWayOperationState state)
	{
		// Java parity breadcrumb: forgetObjectsOrUpdateVisibility deletes owner-side
		// knowledge first, then deletes owner from the other object's known-list.
		if (!state.OwnerKnowsCandidate && !state.CandidateKnowsOwner)
			return CreatePlan(PlayerKnownListTwoWayOperationKind.Remove, PlayerKnownListTwoWayOperationStatus.NothingToRemove, state, []);

		var steps = new List<PlayerKnownListTwoWayOperationStep>();
		if (state.OwnerKnowsCandidate)
		{
			steps.Add(new PlayerKnownListTwoWayOperationStep(
				PlayerKnownListTwoWayOperationStepKind.OwnerRemovesCandidate,
				state.OwnerPlayerObjectId,
				state.CandidatePlayerObjectId,
				"com.aionemu.gameserver.world.knownlist.KnownList.forgetObjectsOrUpdateVisibility -> del(object, ObjectDeleteAnimation.NONE)"));
			if (state.OwnerSeesCandidate)
			{
				steps.Add(new PlayerKnownListTwoWayOperationStep(
					PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate,
					state.OwnerPlayerObjectId,
					state.CandidatePlayerObjectId,
					"com.aionemu.gameserver.world.knownlist.KnownList.del -> notifyNotSee(object, ObjectDeleteAnimation.NONE)"));
			}

			steps.Add(new PlayerKnownListTwoWayOperationStep(
				PlayerKnownListTwoWayOperationStepKind.OwnerNotKnowsCandidate,
				state.OwnerPlayerObjectId,
				state.CandidatePlayerObjectId,
				"com.aionemu.gameserver.world.knownlist.KnownList.del -> notifyNotKnow(object)"));
		}

		if (state.CandidateKnowsOwner)
		{
			steps.Add(new PlayerKnownListTwoWayOperationStep(
				PlayerKnownListTwoWayOperationStepKind.CandidateRemovesOwner,
				state.OwnerPlayerObjectId,
				state.CandidatePlayerObjectId,
				"com.aionemu.gameserver.world.knownlist.KnownList.forgetObjectsOrUpdateVisibility -> object.getKnownList().del(owner, ObjectDeleteAnimation.NONE)"));
			if (state.CandidateSeesOwner)
			{
				steps.Add(new PlayerKnownListTwoWayOperationStep(
					PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner,
					state.OwnerPlayerObjectId,
					state.CandidatePlayerObjectId,
					"com.aionemu.gameserver.world.knownlist.KnownList.del -> notifyNotSee(owner, ObjectDeleteAnimation.NONE)"));
			}

			steps.Add(new PlayerKnownListTwoWayOperationStep(
				PlayerKnownListTwoWayOperationStepKind.CandidateNotKnowsOwner,
				state.OwnerPlayerObjectId,
				state.CandidatePlayerObjectId,
				"com.aionemu.gameserver.world.knownlist.KnownList.del -> notifyNotKnow(owner)"));
		}

		return CreatePlan(PlayerKnownListTwoWayOperationKind.Remove, PlayerKnownListTwoWayOperationStatus.Planned, state, steps);
	}

	public PlayerKnownListTwoWayOperationPlan PlanClearPair(PlayerKnownListTwoWayOperationState state)
	{
		// Java parity breadcrumb: KnownList.clear removes owner-side knowledge with
		// ObjectDeleteAnimation.NONE, then removes the owner from the other object
		// with the caller supplied animation.
		if (!state.OwnerKnowsCandidate && !state.CandidateKnowsOwner)
			return CreatePlan(PlayerKnownListTwoWayOperationKind.Clear, PlayerKnownListTwoWayOperationStatus.NothingToRemove, state, []);

		var steps = new List<PlayerKnownListTwoWayOperationStep>();
		if (state.OwnerKnowsCandidate)
		{
			steps.Add(new PlayerKnownListTwoWayOperationStep(
				PlayerKnownListTwoWayOperationStepKind.OwnerRemovesCandidate,
				state.OwnerPlayerObjectId,
				state.CandidatePlayerObjectId,
				"com.aionemu.gameserver.world.knownlist.KnownList.clear -> del(object.get(), ObjectDeleteAnimation.NONE)"));
			if (state.OwnerSeesCandidate)
			{
				steps.Add(new PlayerKnownListTwoWayOperationStep(
					PlayerKnownListTwoWayOperationStepKind.OwnerNotSeesCandidate,
					state.OwnerPlayerObjectId,
					state.CandidatePlayerObjectId,
					"com.aionemu.gameserver.world.knownlist.KnownList.clear -> owner-side notifyNotSee uses ObjectDeleteAnimation.NONE"));
			}

			steps.Add(new PlayerKnownListTwoWayOperationStep(
				PlayerKnownListTwoWayOperationStepKind.OwnerNotKnowsCandidate,
				state.OwnerPlayerObjectId,
				state.CandidatePlayerObjectId,
				"com.aionemu.gameserver.world.knownlist.KnownList.clear -> owner-side notifyNotKnow"));
		}

		if (state.CandidateKnowsOwner)
		{
			steps.Add(new PlayerKnownListTwoWayOperationStep(
				PlayerKnownListTwoWayOperationStepKind.CandidateRemovesOwner,
				state.OwnerPlayerObjectId,
				state.CandidatePlayerObjectId,
				"com.aionemu.gameserver.world.knownlist.KnownList.clear -> object.get().getKnownList().del(owner, animation)"));
			if (state.CandidateSeesOwner)
			{
				steps.Add(new PlayerKnownListTwoWayOperationStep(
					PlayerKnownListTwoWayOperationStepKind.CandidateNotSeesOwner,
					state.OwnerPlayerObjectId,
					state.CandidatePlayerObjectId,
					"com.aionemu.gameserver.world.knownlist.KnownList.clear -> other-side notifyNotSee uses supplied animation"));
			}

			steps.Add(new PlayerKnownListTwoWayOperationStep(
				PlayerKnownListTwoWayOperationStepKind.CandidateNotKnowsOwner,
				state.OwnerPlayerObjectId,
				state.CandidatePlayerObjectId,
				"com.aionemu.gameserver.world.knownlist.KnownList.clear -> other-side notifyNotKnow"));
		}

		return CreatePlan(PlayerKnownListTwoWayOperationKind.Clear, PlayerKnownListTwoWayOperationStatus.Planned, state, steps);
	}

	private static PlayerKnownListTwoWayOperationPlan CreatePlan(
		PlayerKnownListTwoWayOperationKind kind,
		PlayerKnownListTwoWayOperationStatus status,
		PlayerKnownListTwoWayOperationState state,
		IReadOnlyList<PlayerKnownListTwoWayOperationStep> steps) =>
		new(
			kind,
			status,
			state.OwnerPlayerObjectId,
			state.CandidatePlayerObjectId,
			steps,
			MutatesLiveMembership: false,
			RequiresCandidateAddBeforeOwnerAdd: kind == PlayerKnownListTwoWayOperationKind.Add,
			RequiresOwnerRemoveBeforeCandidateRemove: kind is PlayerKnownListTwoWayOperationKind.Remove or PlayerKnownListTwoWayOperationKind.Clear,
			IsJavaRegionKnownListParity: false,
			"Planner for com.aionemu.gameserver.world.knownlist.KnownList two-way add/remove ordering; does not execute live KnownList mutation or controller side effects",
			IsLive: false);

	private static IReadOnlyList<PlayerKnownListTwoWayOperationStep> CreateSeeStepIfVisible(
		bool isVisible,
		PlayerKnownListTwoWayOperationStepKind kind,
		PlayerKnownListTwoWayOperationState state,
		string javaSource) =>
		isVisible
			? [new PlayerKnownListTwoWayOperationStep(kind, state.OwnerPlayerObjectId, state.CandidatePlayerObjectId, javaSource)]
			: [];
}
