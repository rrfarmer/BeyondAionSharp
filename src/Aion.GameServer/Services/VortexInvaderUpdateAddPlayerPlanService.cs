using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class VortexInvaderUpdateAddPlayerPlanService
{
	public VortexInvaderUpdateAddPlayerPlan CreatePlan(
		VortexInvaderUpdatePlayerSnapshot invader,
		IReadOnlyList<VortexInvaderUpdatePlayerSnapshot>? existingInvaders = null,
		VortexInvaderAllianceSnapshot? invaderAlliance = null)
	{
		ArgumentNullException.ThrowIfNull(invader);

		var invaders = existingInvaders ?? [];
		var alliance = invaderAlliance ?? VortexInvaderAllianceSnapshot.Missing;
		if (invaders.Any(existing => existing.PlayerObjectId == invader.PlayerObjectId))
		{
			return new VortexInvaderUpdateAddPlayerPlan(
				VortexInvaderUpdateAddPlayerPlanStatus.AlreadyInvader,
				invader,
				invaders,
				alliance,
				RemovalPlans: [],
				WouldCallAddPlayer: false,
				WouldAddToExistingAlliance: false,
				WouldCreateInvaderAlliance: false,
				CreatedAllianceTeamType: null,
				WouldPutParticipant: false,
				WarningMessage: string.Empty,
				JavaSource: "services/vortex/Invasion.updateInvaders");
		}

		if (alliance.Exists && !alliance.IsDisbanded)
		{
			return new VortexInvaderUpdateAddPlayerPlan(
				VortexInvaderUpdateAddPlayerPlanStatus.AddToExistingAlliance,
				invader,
				invaders,
				alliance,
				RemovalPlans: [],
				WouldCallAddPlayer: true,
				WouldAddToExistingAlliance: true,
				WouldCreateInvaderAlliance: false,
				CreatedAllianceTeamType: null,
				WouldPutParticipant: true,
				WarningMessage: string.Empty,
				JavaSource: "services/vortex/Invasion.updateInvaders -> services/vortex/Invasion.addPlayer(player, true)");
		}

		if (invaders.Count == 1)
		{
			var otherPlayer = invaders[0];
			return new VortexInvaderUpdateAddPlayerPlan(
				VortexInvaderUpdateAddPlayerPlanStatus.CreateInvaderAlliance,
				invader,
				invaders,
				alliance,
				RemovalPlans:
				[
					VortexInvaderUpdateTeamRemovalPlan.FromPlayer(invader),
					VortexInvaderUpdateTeamRemovalPlan.FromPlayer(otherPlayer),
				],
				WouldCallAddPlayer: true,
				WouldAddToExistingAlliance: false,
				WouldCreateInvaderAlliance: true,
				CreatedAllianceTeamType: PlayerAllianceTeamType.AllianceOffence,
				WouldPutParticipant: true,
				WarningMessage: string.Empty,
				JavaSource: "services/vortex/Invasion.updateInvaders -> services/vortex/Invasion.addPlayer(player, true)");
		}

		if (invaders.Count > 1)
		{
			return new VortexInvaderUpdateAddPlayerPlan(
				VortexInvaderUpdateAddPlayerPlanStatus.MissingAllianceTooManyParticipants,
				invader,
				invaders,
				alliance,
				RemovalPlans: [],
				WouldCallAddPlayer: true,
				WouldAddToExistingAlliance: false,
				WouldCreateInvaderAlliance: false,
				CreatedAllianceTeamType: null,
				WouldPutParticipant: false,
				WarningMessage: $"Couldn't add invader:{invader.PlayerObjectId} to invaders (alliance not initialized). Current participants: {invaders.Count}",
				JavaSource: "services/vortex/Invasion.updateInvaders -> services/vortex/Invasion.addPlayer(player, true)");
		}

		return new VortexInvaderUpdateAddPlayerPlan(
			VortexInvaderUpdateAddPlayerPlanStatus.RecordFirstInvader,
			invader,
			invaders,
			alliance,
			RemovalPlans: [],
			WouldCallAddPlayer: true,
			WouldAddToExistingAlliance: false,
			WouldCreateInvaderAlliance: false,
			CreatedAllianceTeamType: null,
			WouldPutParticipant: true,
			WarningMessage: string.Empty,
			JavaSource: "services/vortex/Invasion.updateInvaders -> services/vortex/Invasion.addPlayer(player, true)");
	}
}

public enum VortexInvaderUpdateAddPlayerPlanStatus
{
	AlreadyInvader,
	RecordFirstInvader,
	AddToExistingAlliance,
	CreateInvaderAlliance,
	MissingAllianceTooManyParticipants,
}

public sealed record VortexInvaderUpdateAddPlayerPlan(
	VortexInvaderUpdateAddPlayerPlanStatus Status,
	VortexInvaderUpdatePlayerSnapshot Invader,
	IReadOnlyList<VortexInvaderUpdatePlayerSnapshot> ExistingInvaders,
	VortexInvaderAllianceSnapshot InvaderAlliance,
	IReadOnlyList<VortexInvaderUpdateTeamRemovalPlan> RemovalPlans,
	bool WouldCallAddPlayer,
	bool WouldAddToExistingAlliance,
	bool WouldCreateInvaderAlliance,
	PlayerAllianceTeamType? CreatedAllianceTeamType,
	bool WouldPutParticipant,
	string WarningMessage,
	string JavaSource)
{
	public IReadOnlyList<int> ExistingInvaderObjectIds => ExistingInvaders.Select(invader => invader.PlayerObjectId).ToArray();
	public bool WouldWarn => !string.IsNullOrEmpty(WarningMessage);
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveInvaders => false;
}

public sealed record VortexInvaderUpdatePlayerSnapshot(
	int PlayerObjectId,
	bool IsInGroup,
	bool IsInAlliance)
{
	public static VortexInvaderUpdatePlayerSnapshot FromPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);
		return new VortexInvaderUpdatePlayerSnapshot(
			player.ObjectId,
			player.TeamMembership == PlayerTeamMembership.Group,
			player.TeamMembership == PlayerTeamMembership.Alliance);
	}
}

public sealed record VortexInvaderUpdateTeamRemovalPlan(
	int PlayerObjectId,
	bool WouldRemoveGroup,
	bool WouldRemoveAlliance)
{
	public static VortexInvaderUpdateTeamRemovalPlan FromPlayer(VortexInvaderUpdatePlayerSnapshot player)
	{
		ArgumentNullException.ThrowIfNull(player);
		return new VortexInvaderUpdateTeamRemovalPlan(
			player.PlayerObjectId,
			player.IsInGroup,
			WouldRemoveAlliance: !player.IsInGroup && player.IsInAlliance);
	}
}

public sealed record VortexInvaderAllianceSnapshot(
	bool Exists,
	bool IsDisbanded = false)
{
	public static VortexInvaderAllianceSnapshot Missing { get; } = new(Exists: false);
	public static VortexInvaderAllianceSnapshot Open { get; } = new(Exists: true);
	public static VortexInvaderAllianceSnapshot Disbanded { get; } = new(Exists: true, IsDisbanded: true);
}
