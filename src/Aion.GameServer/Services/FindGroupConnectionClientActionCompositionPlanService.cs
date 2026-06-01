using Aion.GameServer.Dataholders;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class FindGroupConnectionClientActionCompositionPlanService
{
	private readonly FindGroupClientActionPlanService _planner;
	private readonly GameWorld? _world;
	private readonly PlayerGroupRuntime? _groupRuntime;
	private readonly PlayerAllianceRuntime? _allianceRuntime;
	private readonly AutoGroupTable? _autoGroups;
	private readonly GameServerOptions? _options;

	public FindGroupConnectionClientActionCompositionPlanService(
		FindGroupClientActionPlanService planner,
		GameWorld? world = null,
		PlayerGroupRuntime? groupRuntime = null,
		PlayerAllianceRuntime? allianceRuntime = null,
		AutoGroupTable? autoGroups = null,
		GameServerOptions? options = null)
	{
		_planner = planner;
		_world = world;
		_groupRuntime = groupRuntime;
		_allianceRuntime = allianceRuntime;
		_autoGroups = autoGroups;
		_options = options;
	}

	public FindGroupConnectionClientActionCompositionPlan CreateDisabledPlan(
		GameServerConnection connection,
		CmFindGroup packet,
		int nowEpochSeconds,
		Func<int, Player?>? resolvePlayer = null,
		FindGroupRecruitmentSubject? currentTeam = null,
		IReadOnlyList<FindGroupInstanceGroupMemberState>? currentMembers = null,
		bool? formInstanceGroupAnywhere = null,
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
		bool? formInstanceGroupAnywhere = null,
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

		currentTeam ??= ResolveCurrentTeam(activePlayer);
		currentMembers ??= ResolveCurrentMembers(activePlayer);
		targetNpcInstanceMaskIds ??= ResolveTargetNpcInstanceMaskIds(activePlayer);
		allRecruitableInstanceMaskIds ??= _autoGroups?.GetRecruitableInstanceMaskIds();
		var resolvedFormInstanceGroupAnywhere = formInstanceGroupAnywhere ?? _options?.Instance.FormInstanceGroupAnywhere ?? false;
		var facts = new FindGroupClientActionRuntimeFacts(
			activePlayer,
			nowEpochSeconds,
			resolvePlayer ?? ResolveWorldPlayer,
			currentTeam,
			currentMembers,
			resolvedFormInstanceGroupAnywhere,
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

	private FindGroupRecruitmentSubject? ResolveCurrentTeam(Player player)
	{
		var teamId = player.CurrentTeamId;
		if (teamId == 0 || player.TeamMembership == PlayerTeamMembership.None)
			return null;

		var members = ResolveCurrentTeamPlayers(player);
		if (members.Count == 0)
			return null;

		var leaderObjectId = player.TeamMembership switch
		{
			PlayerTeamMembership.Group => _groupRuntime?.GetDescriptor(teamId)?.LeaderObjectId,
			PlayerTeamMembership.Alliance => _allianceRuntime?.GetDescriptor(teamId)?.LeaderObjectId
				?? player.CurrentAllianceSnapshot?.LeaderObjectId,
			_ => null,
		};
		var leader = leaderObjectId.HasValue
			? members.FirstOrDefault(member => member.ObjectId == leaderObjectId.Value)
			: null;
		leader ??= members[0];

		// Java parity: GroupRecruitment built from TemporaryPlayerTeam reports team id, leader
		// name/class, member count, race, and min/max member levels.
		return new FindGroupRecruitmentSubject(
			teamId,
			leader.Race,
			IsSoloPlayer: false,
			leader.Name,
			members.Count,
			members.Min(member => member.Level),
			members.Max(member => member.Level),
			FindGroupRecruitmentSubject.ToJavaClassId(leader.PlayerClass));
	}

	private IReadOnlyList<FindGroupInstanceGroupMemberState>? ResolveCurrentMembers(Player player)
	{
		if (player.CurrentTeamId == 0 || player.TeamMembership == PlayerTeamMembership.None)
			return null;

		var members = ResolveCurrentTeamPlayers(player);
		return members.Count == 0
			? null
			: members.Select(FindGroupInstanceGroupMemberState.FromPlayer).ToArray();
	}

	private IReadOnlyList<Player> ResolveCurrentTeamPlayers(Player player)
	{
		var teamId = player.CurrentTeamId;
		var objectIds = player.TeamMembership switch
		{
			PlayerTeamMembership.Group => _groupRuntime?.GetMemberObjectIds(teamId),
			PlayerTeamMembership.Alliance => _allianceRuntime?.GetMemberObjectIds(teamId),
			_ => null,
		};
		objectIds = objectIds is { Count: > 0 } ? objectIds : player.CurrentTeamMemberObjectIds;
		if (objectIds.Count == 0)
			return Array.Empty<Player>();

		var members = new List<Player>(objectIds.Count);
		foreach (var objectId in objectIds)
		{
			var member = objectId == player.ObjectId ? player : ResolveWorldPlayer(objectId);
			if (member != null)
				members.Add(member);
		}

		return members;
	}

	private IReadOnlyList<int>? ResolveTargetNpcInstanceMaskIds(Player player)
	{
		// Java parity: FindGroupService.showInstanceGroups checks player.getTarget() instanceof Npc
		// before asking DataManager.AUTO_GROUP.getRecruitableInstanceMaskIds(npc.getNpcId()).
		return _autoGroups != null
			&& _world != null
			&& player.TargetObjectId != 0
			&& _world.TryGetObject(player.TargetObjectId, out var gameObject)
			&& gameObject is IWorldNpcObject npc
				? _autoGroups.GetRecruitableInstanceMaskIds(npc.TemplateId)
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
