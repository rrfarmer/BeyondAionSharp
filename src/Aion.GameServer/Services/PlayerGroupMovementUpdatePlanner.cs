using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class PlayerGroupMovementUpdatePlanner(PlayerGroupRuntime groups)
{
	public PlayerGroupMovementUpdateCallerPlan CreateTeamMoveUpdatePlan(Player player)
	{
		// Java parity: taskmanager/tasks/TeamMoveUpdater.callTask gates on Player.isOnline before PlayerGroupService.updateGroup(..., MOVEMENT).
		return CreatePlan(PlayerGroupMovementUpdateTrigger.TeamMoveUpdater, player, requireOnline: true);
	}

	public PlayerGroupMovementUpdateCallerPlan CreateTeamStatUpdatePlan(Player player)
	{
		// Java parity: taskmanager/tasks/TeamStatUpdater.callTask gates on Player.isOnline before PlayerGroupService.updateGroup(..., MOVEMENT).
		return CreatePlan(PlayerGroupMovementUpdateTrigger.TeamStatUpdater, player, requireOnline: true);
	}

	public PlayerGroupMovementUpdateCallerPlan CreateEffectMovementUpdatePlan(Player player)
	{
		// Java parity: controllers/effect/PlayerEffectController.updatePlayerIconsAndGroup calls PlayerGroupService.updateGroup(..., MOVEMENT).
		return CreatePlan(PlayerGroupMovementUpdateTrigger.PlayerEffectController, player, requireOnline: false);
	}

	public PlayerGroupMovementUpdateCallerPlan CreateReviveMovementUpdatePlan(Player player)
	{
		// Java parity: services/player/PlayerReviveService.revive calls PlayerGroupService.updateGroup(..., MOVEMENT).
		return CreatePlan(PlayerGroupMovementUpdateTrigger.PlayerReviveService, player, requireOnline: false);
	}

	private PlayerGroupMovementUpdateCallerPlan CreatePlan(
		PlayerGroupMovementUpdateTrigger trigger,
		Player player,
		bool requireOnline)
	{
		if (requireOnline && !player.IsOnline)
			return Skipped(trigger, player, PlayerGroupMovementUpdateStatus.Offline);

		if (player.TeamMembership == PlayerTeamMembership.Alliance)
			return Skipped(trigger, player, PlayerGroupMovementUpdateStatus.AllianceDeferred);

		var teamId = player.CurrentGroupSnapshot?.TeamId
			?? (player.TeamMembership == PlayerTeamMembership.Group ? player.CurrentTeamId : 0);
		if (teamId == 0)
			return Skipped(trigger, player, PlayerGroupMovementUpdateStatus.NotInGroup);

		var updatePlan = groups.CreateMemberInfoUpdatePlan(teamId, player, PlayerGroupEvent.Movement);
		if (updatePlan == null)
			return Skipped(trigger, player, PlayerGroupMovementUpdateStatus.MissingGroup);

		return new PlayerGroupMovementUpdateCallerPlan(
			trigger,
			player.ObjectId,
			PlayerGroupMovementUpdateStatus.Planned,
			updatePlan);
	}

	private static PlayerGroupMovementUpdateCallerPlan Skipped(
		PlayerGroupMovementUpdateTrigger trigger,
		Player player,
		PlayerGroupMovementUpdateStatus status)
	{
		return new PlayerGroupMovementUpdateCallerPlan(trigger, player.ObjectId, status, MemberInfoUpdatePlan: null);
	}
}

public sealed record PlayerGroupMovementUpdateCallerPlan(
	PlayerGroupMovementUpdateTrigger Trigger,
	int PlayerObjectId,
	PlayerGroupMovementUpdateStatus Status,
	PlayerGroupMemberInfoUpdatePlan? MemberInfoUpdatePlan)
{
	public bool IsPlanned => Status == PlayerGroupMovementUpdateStatus.Planned;
}

public enum PlayerGroupMovementUpdateTrigger
{
	TeamMoveUpdater,
	TeamStatUpdater,
	PlayerEffectController,
	PlayerReviveService,
}

public enum PlayerGroupMovementUpdateStatus
{
	Planned,
	Offline,
	NotInGroup,
	AllianceDeferred,
	MissingGroup,
}
