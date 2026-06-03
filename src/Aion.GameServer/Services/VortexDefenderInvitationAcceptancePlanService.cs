using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class VortexDefenderInvitationAcceptancePlanService
{
	public VortexDefenderInvitationAcceptancePlan CreatePlan(
		VortexDefenderInvitationResponderSnapshot responder,
		VortexDefenderAllianceSnapshot? defenderAlliance = null)
	{
		ArgumentNullException.ThrowIfNull(responder);

		var alliance = defenderAlliance ?? VortexDefenderAllianceSnapshot.Missing;
		var wouldRemoveGroup = responder.IsInGroup;
		var wouldRemoveAlliance = !wouldRemoveGroup && responder.IsInAlliance;
		var wouldAddDefender = !alliance.IsFull;
		var status = wouldAddDefender
			? VortexDefenderInvitationAcceptancePlanStatus.AcceptancePlanned
			: VortexDefenderInvitationAcceptancePlanStatus.DefenderAllianceFull;

		return new VortexDefenderInvitationAcceptancePlan(
			status,
			responder,
			alliance,
			wouldRemoveGroup,
			wouldRemoveAlliance,
			wouldAddDefender,
			JavaSource: "services/vortex/Invasion.updateDefenders.RequestResponseHandler.acceptRequest");
	}
}

public enum VortexDefenderInvitationAcceptancePlanStatus
{
	AcceptancePlanned,
	DefenderAllianceFull,
}

public sealed record VortexDefenderInvitationAcceptancePlan(
	VortexDefenderInvitationAcceptancePlanStatus Status,
	VortexDefenderInvitationResponderSnapshot Responder,
	VortexDefenderAllianceSnapshot DefenderAlliance,
	bool WouldRemoveGroup,
	bool WouldRemoveAlliance,
	bool WouldAddDefender,
	string JavaSource)
{
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed record VortexDefenderInvitationResponderSnapshot(
	int PlayerObjectId,
	bool IsInGroup,
	bool IsInAlliance)
{
	public static VortexDefenderInvitationResponderSnapshot FromPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);
		return new VortexDefenderInvitationResponderSnapshot(
			player.ObjectId,
			player.TeamMembership == PlayerTeamMembership.Group,
			player.TeamMembership == PlayerTeamMembership.Alliance);
	}
}
