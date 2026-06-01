using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class FindGroupConnectionClientActionCompositionPlanService
{
	private readonly FindGroupClientActionPlanService _planner;
	private readonly GameWorld? _world;

	public FindGroupConnectionClientActionCompositionPlanService(
		FindGroupClientActionPlanService planner,
		GameWorld? world = null)
	{
		_planner = planner;
		_world = world;
	}

	public FindGroupConnectionClientActionCompositionPlan CreateDisabledPlan(
		GameServerConnection connection,
		CmFindGroup packet,
		int nowEpochSeconds,
		Func<int, Player?>? resolvePlayer = null,
		FindGroupRecruitmentSubject? currentTeam = null,
		IReadOnlyList<FindGroupInstanceGroupMemberState>? currentMembers = null,
		bool formInstanceGroupAnywhere = false,
		IReadOnlyList<int>? targetNpcInstanceMaskIds = null,
		IReadOnlyList<int>? allRecruitableInstanceMaskIds = null)
	{
		return CreateDisabledPlan(
			connection.ActivePlayer,
			packet,
			nowEpochSeconds,
			resolvePlayer ?? ResolveWorldPlayer,
			currentTeam,
			currentMembers,
			formInstanceGroupAnywhere,
			targetNpcInstanceMaskIds,
			allRecruitableInstanceMaskIds);
	}

	public FindGroupConnectionClientActionCompositionPlan CreateDisabledPlan(
		Player? activePlayer,
		CmFindGroup packet,
		int nowEpochSeconds,
		Func<int, Player?>? resolvePlayer = null,
		FindGroupRecruitmentSubject? currentTeam = null,
		IReadOnlyList<FindGroupInstanceGroupMemberState>? currentMembers = null,
		bool formInstanceGroupAnywhere = false,
		IReadOnlyList<int>? targetNpcInstanceMaskIds = null,
		IReadOnlyList<int>? allRecruitableInstanceMaskIds = null)
	{
		var action = FindGroupClientAction.FromPacket(packet);
		if (activePlayer == null)
		{
			// Java parity: CM_FIND_GROUP.runImpl asks the connection for getActivePlayer().
			// This disabled adapter records the missing-player boundary without sending packets.
			return FindGroupConnectionClientActionCompositionPlan.SkippedMissingActivePlayer(action);
		}

		var facts = new FindGroupClientActionRuntimeFacts(
			activePlayer,
			nowEpochSeconds,
			resolvePlayer ?? ResolveWorldPlayer,
			currentTeam,
			currentMembers,
			formInstanceGroupAnywhere,
			targetNpcInstanceMaskIds,
			allRecruitableInstanceMaskIds);
		return FindGroupConnectionClientActionCompositionPlan.Composed(
			activePlayer,
			action,
			facts.ComposeDisabledPlan(_planner, action));
	}

	private Player? ResolveWorldPlayer(int objectId)
	{
		// Java parity: FindGroupService.sendInstanceApplication and sendInstanceApplicationResult
		// call World.getInstance().getPlayer(objectId). The disabled adapter reads the C# world
		// snapshot when supplied, but still only composes non-live side-effect plans.
		return _world != null
			&& _world.TryGetObject(objectId, out var gameObject)
			&& gameObject is Player player
				? player
				: null;
	}
}

public enum FindGroupConnectionClientActionCompositionStatus
{
	ComposedDisabledPlan,
	SkippedMissingActivePlayer,
}

public sealed record FindGroupConnectionClientActionCompositionPlan(
	FindGroupConnectionClientActionCompositionStatus Status,
	Player? ActivePlayer,
	FindGroupClientAction Action,
	FindGroupClientActionPlan? ClientActionPlan,
	bool ShouldDispatchLiveSideEffects)
{
	public static FindGroupConnectionClientActionCompositionPlan Composed(
		Player activePlayer,
		FindGroupClientAction action,
		FindGroupClientActionPlan clientActionPlan)
	{
		return new FindGroupConnectionClientActionCompositionPlan(
			FindGroupConnectionClientActionCompositionStatus.ComposedDisabledPlan,
			activePlayer,
			action,
			clientActionPlan,
			ShouldDispatchLiveSideEffects: false);
	}

	public static FindGroupConnectionClientActionCompositionPlan SkippedMissingActivePlayer(FindGroupClientAction action)
	{
		return new FindGroupConnectionClientActionCompositionPlan(
			FindGroupConnectionClientActionCompositionStatus.SkippedMissingActivePlayer,
			ActivePlayer: null,
			action,
			ClientActionPlan: null,
			ShouldDispatchLiveSideEffects: false);
	}
}
