namespace Aion.GameServer.Services;

public sealed class VortexInvaderPassedPortalUpdatePlanService
{
	private const string ZoneEntryJavaSource = "model/vortex/VortexLocation.onEnterZone";
	private const string AddPlayerJavaSource = "model/vortex/VortexLocation.onEnterZone -> services/vortex/Invasion.addPlayer(player, true)";

	private readonly VortexInvaderUpdateAddPlayerPlanService _invaderPlanner = new();

	public VortexInvaderPassedPortalUpdatePlan CreatePlan(
		int locationId,
		VortexInvaderUpdatePlayerSnapshot invader,
		bool hasActiveInvasion,
		IReadOnlySet<int>? passedPlayerObjectIds = null,
		IReadOnlyList<VortexInvaderUpdatePlayerSnapshot>? existingInvaders = null,
		VortexInvaderAllianceSnapshot? invaderAlliance = null,
		bool isNewZonePlayer = true,
		bool isInvaderRace = true)
	{
		ArgumentNullException.ThrowIfNull(invader);

		var passedPlayers = passedPlayerObjectIds?.ToArray() ?? [];
		var invaders = existingInvaders ?? [];
		var alliance = invaderAlliance ?? VortexInvaderAllianceSnapshot.Missing;
		var hadPassedPortal = passedPlayers.Contains(invader.PlayerObjectId);

		if (!isNewZonePlayer)
		{
			return CreateGuardPlan(
				VortexInvaderPassedPortalUpdatePlanStatus.NotNewZonePlayer,
				locationId,
				invader,
				hasActiveInvasion,
				passedPlayers,
				invaders,
				alliance,
				isNewZonePlayer,
				isInvaderRace,
				hadPassedPortal);
		}

		if (!hasActiveInvasion)
		{
			return CreateGuardPlan(
				VortexInvaderPassedPortalUpdatePlanStatus.InactiveVortex,
				locationId,
				invader,
				hasActiveInvasion,
				passedPlayers,
				invaders,
				alliance,
				isNewZonePlayer,
				isInvaderRace,
				hadPassedPortal);
		}

		if (!isInvaderRace)
		{
			return CreateGuardPlan(
				VortexInvaderPassedPortalUpdatePlanStatus.NonInvaderRace,
				locationId,
				invader,
				hasActiveInvasion,
				passedPlayers,
				invaders,
				alliance,
				isNewZonePlayer,
				isInvaderRace,
				hadPassedPortal);
		}

		if (!hadPassedPortal)
		{
			return CreateGuardPlan(
				VortexInvaderPassedPortalUpdatePlanStatus.MissingPassedPlayer,
				locationId,
				invader,
				hasActiveInvasion,
				passedPlayers,
				invaders,
				alliance,
				isNewZonePlayer,
				isInvaderRace,
				hadPassedPortal);
		}

		var invaderPlan = _invaderPlanner.CreatePlan(invader, invaders, alliance);
		var status = invaderPlan.Status == VortexInvaderUpdateAddPlayerPlanStatus.AlreadyInvader
			? VortexInvaderPassedPortalUpdatePlanStatus.AlreadyInvader
			: VortexInvaderPassedPortalUpdatePlanStatus.UpdatePlanned;
		return new VortexInvaderPassedPortalUpdatePlan(
			status,
			locationId,
			invader,
			passedPlayers,
			invaders,
			alliance,
			invaderPlan,
			isNewZonePlayer,
			hasActiveInvasion,
			isInvaderRace,
			hadPassedPortal,
			invaderPlan.WouldCallAddPlayer,
			AddPlayerJavaSource);
	}

	private static VortexInvaderPassedPortalUpdatePlan CreateGuardPlan(
		VortexInvaderPassedPortalUpdatePlanStatus status,
		int locationId,
		VortexInvaderUpdatePlayerSnapshot invader,
		bool hasActiveInvasion,
		IReadOnlyList<int> passedPlayerObjectIds,
		IReadOnlyList<VortexInvaderUpdatePlayerSnapshot> existingInvaders,
		VortexInvaderAllianceSnapshot invaderAlliance,
		bool isNewZonePlayer,
		bool isInvaderRace,
		bool hadPassedPortal)
	{
		return new VortexInvaderPassedPortalUpdatePlan(
			status,
			locationId,
			invader,
			passedPlayerObjectIds,
			existingInvaders,
			invaderAlliance,
			InvaderUpdatePlan: null,
			isNewZonePlayer,
			hasActiveInvasion,
			isInvaderRace,
			hadPassedPortal,
			WouldCallAddPlayer: false,
			ZoneEntryJavaSource);
	}
}

public enum VortexInvaderPassedPortalUpdatePlanStatus
{
	NotNewZonePlayer,
	InactiveVortex,
	NonInvaderRace,
	MissingPassedPlayer,
	AlreadyInvader,
	UpdatePlanned,
}

public sealed record VortexInvaderPassedPortalUpdatePlan(
	VortexInvaderPassedPortalUpdatePlanStatus Status,
	int LocationId,
	VortexInvaderUpdatePlayerSnapshot Invader,
	IReadOnlyList<int> PassedPlayerObjectIds,
	IReadOnlyList<VortexInvaderUpdatePlayerSnapshot> ExistingInvaders,
	VortexInvaderAllianceSnapshot InvaderAlliance,
	VortexInvaderUpdateAddPlayerPlan? InvaderUpdatePlan,
	bool IsNewZonePlayer,
	bool HasActiveInvasion,
	bool IsInvaderRace,
	bool HadPassedPortal,
	bool WouldCallAddPlayer,
	string JavaSource)
{
	public int PlayerObjectId => Invader.PlayerObjectId;
	public IReadOnlyList<int> ExistingInvaderObjectIds => ExistingInvaders.Select(invader => invader.PlayerObjectId).ToArray();
	public bool HasInvaderUpdatePlan => InvaderUpdatePlan is not null;
	public bool ShouldRecordZonePlayer => false;
	public bool ShouldMutateLiveInvaders => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldMutateLiveGroup => false;
}
