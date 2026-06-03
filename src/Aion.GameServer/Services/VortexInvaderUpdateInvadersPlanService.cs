namespace Aion.GameServer.Services;

public sealed class VortexInvaderUpdateInvadersPlanService
{
	private readonly VortexInvaderUpdateAddPlayerPlanService _addPlayerPlanner = new();

	public VortexInvaderUpdateInvadersPlan CreatePlan(
		VortexInvaderUpdatePlayerSnapshot invader,
		IReadOnlyList<VortexInvaderUpdatePlayerSnapshot>? existingInvaders = null,
		VortexInvaderAllianceSnapshot? invaderAlliance = null)
	{
		ArgumentNullException.ThrowIfNull(invader);

		var invaders = existingInvaders ?? [];
		var alliance = invaderAlliance ?? VortexInvaderAllianceSnapshot.Missing;
		var addPlayerPlan = _addPlayerPlanner.CreatePlan(invader, invaders, alliance);
		var status = addPlayerPlan.Status switch
		{
			VortexInvaderUpdateAddPlayerPlanStatus.AlreadyInvader => VortexInvaderUpdateInvadersPlanStatus.AlreadyInvader,
			VortexInvaderUpdateAddPlayerPlanStatus.MissingAllianceTooManyParticipants => VortexInvaderUpdateInvadersPlanStatus.AddPlayerWarning,
			_ => VortexInvaderUpdateInvadersPlanStatus.AddPlayerPlanned,
		};

		return new VortexInvaderUpdateInvadersPlan(
			status,
			invader,
			invaders,
			alliance,
			addPlayerPlan,
			JavaSource: addPlayerPlan.Status == VortexInvaderUpdateAddPlayerPlanStatus.AlreadyInvader
				? "services/vortex/Invasion.updateInvaders"
				: "services/vortex/Invasion.updateInvaders -> services/vortex/Invasion.addPlayer(player, true)");
	}
}

public enum VortexInvaderUpdateInvadersPlanStatus
{
	AlreadyInvader,
	AddPlayerPlanned,
	AddPlayerWarning,
}

public sealed record VortexInvaderUpdateInvadersPlan(
	VortexInvaderUpdateInvadersPlanStatus Status,
	VortexInvaderUpdatePlayerSnapshot Invader,
	IReadOnlyList<VortexInvaderUpdatePlayerSnapshot> ExistingInvaders,
	VortexInvaderAllianceSnapshot InvaderAlliance,
	VortexInvaderUpdateAddPlayerPlan AddPlayerPlan,
	string JavaSource)
{
	public int PlayerObjectId => Invader.PlayerObjectId;
	public IReadOnlyList<int> ExistingInvaderObjectIds => ExistingInvaders.Select(invader => invader.PlayerObjectId).ToArray();
	public bool WouldCallAddPlayer => AddPlayerPlan.WouldCallAddPlayer;
	public bool WouldAddToExistingAlliance => AddPlayerPlan.WouldAddToExistingAlliance;
	public bool WouldCreateInvaderAlliance => AddPlayerPlan.WouldCreateInvaderAlliance;
	public PlayerAllianceTeamType? CreatedAllianceTeamType => AddPlayerPlan.CreatedAllianceTeamType;
	public bool WouldPutParticipant => AddPlayerPlan.WouldPutParticipant;
	public bool WouldWarn => AddPlayerPlan.WouldWarn;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveGroup => false;
	public bool ShouldMutateLiveInvaders => false;
}
