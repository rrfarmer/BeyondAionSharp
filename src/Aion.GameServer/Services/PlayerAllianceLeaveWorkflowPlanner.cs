using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceLeaveWorkflowPlanner(
	PlayerAllianceLeavedPlanner? alliancePlanner = null,
	PlayerBaseLeavePlanner? baseLeavePlanner = null)
{
	private readonly PlayerAllianceLeavedPlanner _alliancePlanner = alliancePlanner ?? new PlayerAllianceLeavedPlanner();
	private readonly PlayerBaseLeavePlanner _baseLeavePlanner = baseLeavePlanner ?? new PlayerBaseLeavePlanner();

	public PlayerAllianceLeaveWorkflowPlan CreateLeaveWorkflowPlan(
		int allianceId,
		int leaderObjectId,
		IReadOnlyList<Player> membersAfterLeave,
		Player leavedPlayer,
		IReadOnlyList<int> currentViceCaptainObjectIds,
		PlayerAllianceLeaveReason reason = PlayerAllianceLeaveReason.Leave,
		string banPersonName = "",
		PlayerGroupLootRules? lootRules = null,
		PlayerAllianceTeamType teamType = PlayerAllianceTeamType.Alliance,
		bool leavedPlayerWasLeader = false,
		bool shouldDisband = false,
		bool isInLeague = false,
		bool wasRegisteredToTeamInstance = false)
	{
		// Java parity: PlayerAllianceLeavedEvent.handleEvent performs alliance leave fanout, then calls PlayerLeavedEvent.handleEvent.
		var allianceLeavePlan = _alliancePlanner.CreateLeavedPlan(
			allianceId,
			leaderObjectId,
			membersAfterLeave,
			leavedPlayer,
			currentViceCaptainObjectIds,
			reason,
			banPersonName,
			lootRules,
			teamType,
			leavedPlayerWasLeader,
			shouldDisband,
			isInLeague);
		var baseLeavePlan = _baseLeavePlanner.CreateLeaveSideEffectPlan(
			leavedPlayer.ObjectId,
			leavedPlayer.IsOnline,
			wasRegisteredToTeamInstance);

		return new PlayerAllianceLeaveWorkflowPlan(
			allianceId,
			leavedPlayer.ObjectId,
			allianceLeavePlan,
			baseLeavePlan,
			[
				new PlayerAllianceLeaveWorkflowStep(0, PlayerAllianceLeaveWorkflowStepKind.AllianceLeave),
				new PlayerAllianceLeaveWorkflowStep(1, PlayerAllianceLeaveWorkflowStepKind.BaseLeave),
			]);
	}
}
