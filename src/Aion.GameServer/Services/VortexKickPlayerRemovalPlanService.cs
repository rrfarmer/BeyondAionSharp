using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class VortexKickPlayerRemovalPlanService
{
	public const int InvaderAllianceKickMessageId = 1401452;
	public const int DefenderAllianceKickMessageId = 1401476;
	public const int InvaderDirectPortalOutMessageId = 1401474;

	public VortexKickPlayerRemovalPlan CreatePlan(
		int locationId,
		VortexKickPlayerSnapshot player,
		bool isInvader,
		bool isParticipant,
		VortexKickPlayerAllianceSnapshot? alliance = null,
		IReadOnlySet<int>? passedPlayerObjectIds = null,
		int passedPlayerCountAfterRemoval = 0,
		int invasionWorldId = 0,
		WorldPosition? homePoint = null)
	{
		ArgumentNullException.ThrowIfNull(player);

		var allianceSnapshot = alliance ?? VortexKickPlayerAllianceSnapshot.Missing;
		var passedPlayers = passedPlayerObjectIds?.ToArray() ?? [];
		var removedPassedPlayer = passedPlayers.Contains(player.PlayerObjectId);
		var wouldRemoveFromAlliance = allianceSnapshot.Exists && allianceSnapshot.HasMember;
		var wouldSendAllianceKickMessage = wouldRemoveFromAlliance && player.IsOnline;
		var wouldClearAllianceReference = wouldRemoveFromAlliance && allianceSnapshot.IsDisbandedAfterRemoval;
		var wasInInvasionWorld = player.WorldId == invasionWorldId;
		var wouldSendDirectPortalOutMessage = isInvader && player.IsOnline && wasInInvasionWorld;
		var wouldTeleportHome = wouldSendDirectPortalOutMessage;
		var status = ResolveStatus(isInvader, isParticipant, wouldRemoveFromAlliance, wouldTeleportHome);

		return new VortexKickPlayerRemovalPlan(
			status,
			locationId,
			player,
			isInvader,
			isParticipant,
			allianceSnapshot,
			passedPlayers,
			removedPassedPlayer,
			passedPlayerCountAfterRemoval,
			invasionWorldId,
			homePoint,
			WouldRemoveParticipant: isParticipant,
			wouldRemoveFromAlliance,
			AllianceKickMessageId: wouldSendAllianceKickMessage
				? isInvader ? InvaderAllianceKickMessageId : DefenderAllianceKickMessageId
				: null,
			wouldClearAllianceReference,
			wouldSendDirectPortalOutMessage ? InvaderDirectPortalOutMessageId : null,
			wouldTeleportHome,
			WouldRemovePassedPlayer: removedPassedPlayer,
			WouldSyncPassedPlayers: true,
			PassedPlayerSyncPlan: new VortexPassedPlayerSyncPlan(
				locationId,
				passedPlayerCountAfterRemoval,
				UsePassedPlayerCount: true,
				"services/vortex/Invasion.kickPlayer -> controllers/RVController.syncPassed(true)"),
			JavaSource: "services/vortex/Invasion.kickPlayer");
	}

	private static VortexKickPlayerRemovalPlanStatus ResolveStatus(
		bool isInvader,
		bool isParticipant,
		bool wouldRemoveFromAlliance,
		bool wouldTeleportHome)
	{
		if (!isParticipant)
			return VortexKickPlayerRemovalPlanStatus.NotParticipant;

		if (isInvader && wouldTeleportHome)
			return VortexKickPlayerRemovalPlanStatus.InvaderRemovedWithTeleport;

		if (isInvader)
			return VortexKickPlayerRemovalPlanStatus.InvaderRemoved;

		return wouldRemoveFromAlliance
			? VortexKickPlayerRemovalPlanStatus.DefenderRemovedFromAlliance
			: VortexKickPlayerRemovalPlanStatus.DefenderRemoved;
	}
}

public enum VortexKickPlayerRemovalPlanStatus
{
	NotParticipant,
	InvaderRemoved,
	InvaderRemovedWithTeleport,
	DefenderRemoved,
	DefenderRemovedFromAlliance,
}

public sealed record VortexKickPlayerRemovalPlan(
	VortexKickPlayerRemovalPlanStatus Status,
	int LocationId,
	VortexKickPlayerSnapshot Player,
	bool IsInvader,
	bool IsParticipant,
	VortexKickPlayerAllianceSnapshot Alliance,
	IReadOnlyList<int> PassedPlayerObjectIds,
	bool RemovedPassedPlayer,
	int PassedPlayerCountAfterRemoval,
	int InvasionWorldId,
	WorldPosition? HomePoint,
	bool WouldRemoveParticipant,
	bool WouldRemoveFromAlliance,
	int? AllianceKickMessageId,
	bool WouldClearAllianceReference,
	int? DirectPortalOutMessageId,
	bool WouldTeleportHome,
	bool WouldRemovePassedPlayer,
	bool WouldSyncPassedPlayers,
	VortexPassedPlayerSyncPlan PassedPlayerSyncPlan,
	string JavaSource)
{
	public int PlayerObjectId => Player.PlayerObjectId;
	public bool WasOnline => Player.IsOnline;
	public bool WasInInvasionWorld => Player.WorldId == InvasionWorldId;
	public bool ShouldMutateLiveParticipants => false;
	public bool ShouldMutateLiveAlliance => false;
	public bool ShouldSendLivePacket => false;
	public bool ShouldTeleportLivePlayer => false;
	public bool ShouldMutateLivePassedPlayers => false;
	public bool ShouldSyncLivePassedPlayers => false;
}

public sealed record VortexKickPlayerSnapshot(
	int PlayerObjectId,
	bool IsOnline,
	int WorldId);

public sealed record VortexKickPlayerAllianceSnapshot(
	bool Exists,
	bool HasMember,
	bool IsDisbandedAfterRemoval = false)
{
	public static VortexKickPlayerAllianceSnapshot Missing { get; } = new(Exists: false, HasMember: false);
	public static VortexKickPlayerAllianceSnapshot NonMember { get; } = new(Exists: true, HasMember: false);
	public static VortexKickPlayerAllianceSnapshot MemberActive { get; } = new(Exists: true, HasMember: true);
	public static VortexKickPlayerAllianceSnapshot MemberDisbandedAfterRemoval { get; } = new(
		Exists: true,
		HasMember: true,
		IsDisbandedAfterRemoval: true);
}
