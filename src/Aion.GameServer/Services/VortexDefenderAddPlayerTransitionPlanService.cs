using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class VortexDefenderAcceptanceInputResolverService
{
	// Java parity: Invasion.defAlliance is created only after a second defender joins.
	// Max PlayerAlliance size in Java is 24 members.
	public const int DefenderAllianceMaxSize = 24;

	public VortexDefenderAcceptanceInputs Resolve(
		VortexInvasionSnapshot? snapshot,
		Func<int, Player?> playerLookup)
	{
		ArgumentNullException.ThrowIfNull(playerLookup);

		if (snapshot is null)
			return VortexDefenderAcceptanceInputs.Empty;

		var existingDefenders = snapshot.DefenderObjectIds
			.Select(id =>
			{
				var player = playerLookup(id);
				return player is null
					? new VortexDefenderAddPlayerSnapshot(id, IsInGroup: false, IsInAlliance: false)
					: VortexDefenderAddPlayerSnapshot.FromPlayer(player);
			})
			.ToArray();

		// Java parity approximation: defAlliance is null (Missing) until a second defender joins,
		// then it is open. Full once it reaches the PlayerAlliance maximum size (24).
		// C# does not track the live alliance reference; defender count is used as a proxy.
		var defenderCount = existingDefenders.Length;
		var allianceSnapshot = defenderCount switch
		{
			0 => VortexDefenderAllianceSnapshot.Missing,
			1 => VortexDefenderAllianceSnapshot.Missing,
			_ when defenderCount >= DefenderAllianceMaxSize => VortexDefenderAllianceSnapshot.Full,
			_ => VortexDefenderAllianceSnapshot.Open,
		};

		return new VortexDefenderAcceptanceInputs(
			snapshot.LocationId,
			existingDefenders,
			allianceSnapshot,
			JavaSource: "services/vortex/Invasion.defenders / services/vortex/Invasion.defAlliance");
	}
}

public sealed record VortexDefenderAcceptanceInputs(
	int LocationId,
	IReadOnlyList<VortexDefenderAddPlayerSnapshot> ExistingDefenders,
	VortexDefenderAllianceSnapshot DefenderAlliance,
	string JavaSource)
{
	public static VortexDefenderAcceptanceInputs Empty { get; } = new(
		LocationId: 0,
		ExistingDefenders: [],
		DefenderAlliance: VortexDefenderAllianceSnapshot.Missing,
		JavaSource: "services/vortex/Invasion.defenders");

	public int DefenderCount => ExistingDefenders.Count;
	public bool HasExistingDefenders => ExistingDefenders.Count > 0;
}

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
