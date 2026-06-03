using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class VortexDefenderAddPlayerTransitionPlanService
{
	public VortexDefenderAddPlayerTransitionPlan CreatePlan(
		VortexDefenderAddPlayerSnapshot player,
		IReadOnlyList<VortexDefenderAddPlayerSnapshot>? existingDefenders = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null)
	{
		ArgumentNullException.ThrowIfNull(player);

		var defenders = existingDefenders ?? [];
		var alliance = defenderAlliance ?? VortexDefenderAllianceSnapshot.Missing;
		if (alliance.Exists && !alliance.IsDisbanded)
		{
			return new VortexDefenderAddPlayerTransitionPlan(
				VortexDefenderAddPlayerTransitionPlanStatus.AddToExistingAlliance,
				player,
				defenders,
				alliance,
				RemovalPlans: [],
				WouldAddToExistingAlliance: true,
				WouldCreateDefenderAlliance: false,
				CreatedAllianceTeamType: null,
				WouldPutParticipant: true,
				WarningMessage: string.Empty,
				JavaSource: "services/vortex/Invasion.addPlayer(player, false)");
		}

		if (defenders.Count == 1)
		{
			var otherPlayer = defenders[0];
			return new VortexDefenderAddPlayerTransitionPlan(
				VortexDefenderAddPlayerTransitionPlanStatus.CreateDefenderAlliance,
				player,
				defenders,
				alliance,
				RemovalPlans:
				[
					VortexDefenderAddPlayerTeamRemovalPlan.FromPlayer(player),
					VortexDefenderAddPlayerTeamRemovalPlan.FromPlayer(otherPlayer),
				],
				WouldAddToExistingAlliance: false,
				WouldCreateDefenderAlliance: true,
				CreatedAllianceTeamType: PlayerAllianceTeamType.AllianceDefence,
				WouldPutParticipant: true,
				WarningMessage: string.Empty,
				JavaSource: "services/vortex/Invasion.addPlayer(player, false)");
		}

		if (defenders.Count > 1)
		{
			return new VortexDefenderAddPlayerTransitionPlan(
				VortexDefenderAddPlayerTransitionPlanStatus.MissingAllianceTooManyParticipants,
				player,
				defenders,
				alliance,
				RemovalPlans: [],
				WouldAddToExistingAlliance: false,
				WouldCreateDefenderAlliance: false,
				CreatedAllianceTeamType: null,
				WouldPutParticipant: false,
				WarningMessage: $"Couldn't add defender:{player.PlayerObjectId} to defenders (alliance not initialized). Current participants: {defenders.Count}",
				JavaSource: "services/vortex/Invasion.addPlayer(player, false)");
		}

		return new VortexDefenderAddPlayerTransitionPlan(
			VortexDefenderAddPlayerTransitionPlanStatus.RecordFirstDefender,
			player,
			defenders,
			alliance,
			RemovalPlans: [],
			WouldAddToExistingAlliance: false,
			WouldCreateDefenderAlliance: false,
			CreatedAllianceTeamType: null,
			WouldPutParticipant: true,
			WarningMessage: string.Empty,
			JavaSource: "services/vortex/Invasion.addPlayer(player, false)");
	}
}

public enum VortexDefenderAddPlayerTransitionPlanStatus
{
	RecordFirstDefender,
	AddToExistingAlliance,
	CreateDefenderAlliance,
	MissingAllianceTooManyParticipants,
}

public sealed record VortexDefenderAddPlayerTransitionPlan(
	VortexDefenderAddPlayerTransitionPlanStatus Status,
	VortexDefenderAddPlayerSnapshot Player,
	IReadOnlyList<VortexDefenderAddPlayerSnapshot> ExistingDefenders,
	VortexDefenderAllianceSnapshot DefenderAlliance,
	IReadOnlyList<VortexDefenderAddPlayerTeamRemovalPlan> RemovalPlans,
	bool WouldAddToExistingAlliance,
	bool WouldCreateDefenderAlliance,
	PlayerAllianceTeamType? CreatedAllianceTeamType,
	bool WouldPutParticipant,
	string WarningMessage,
	string JavaSource)
{
	public IReadOnlyList<int> ExistingDefenderObjectIds => ExistingDefenders.Select(defender => defender.PlayerObjectId).ToArray();
	public bool WouldWarn => !string.IsNullOrEmpty(WarningMessage);
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveDefenders => false;
}

public sealed record VortexDefenderAddPlayerSnapshot(
	int PlayerObjectId,
	bool IsInGroup,
	bool IsInAlliance)
{
	public static VortexDefenderAddPlayerSnapshot FromPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);
		return new VortexDefenderAddPlayerSnapshot(
			player.ObjectId,
			player.TeamMembership == PlayerTeamMembership.Group,
			player.TeamMembership == PlayerTeamMembership.Alliance);
	}
}

public sealed record VortexDefenderAddPlayerTeamRemovalPlan(
	int PlayerObjectId,
	bool WouldRemoveGroup,
	bool WouldRemoveAlliance)
{
	public static VortexDefenderAddPlayerTeamRemovalPlan FromPlayer(VortexDefenderAddPlayerSnapshot player)
	{
		ArgumentNullException.ThrowIfNull(player);
		return new VortexDefenderAddPlayerTeamRemovalPlan(
			player.PlayerObjectId,
			player.IsInGroup,
			WouldRemoveAlliance: !player.IsInGroup && player.IsInAlliance);
	}
}
