using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class PlayerTargetChangePacketPlanService
{
	public static PlayerTargetChangePacketPlan CreatePlan(Player player, TargetSelectedSnapshot? newTarget)
	{
		ArgumentNullException.ThrowIfNull(player);

		return CreatePlan(player.ObjectId, player.TargetObjectId, newTarget);
	}

	public static PlayerTargetChangePacketPlan CreatePlan(
		int playerObjectId,
		int currentTargetObjectId,
		TargetSelectedSnapshot? newTarget)
	{
		// Java parity breadcrumb: VisibleObject.setTarget calls PlayerController.onTargetChanged
		// only when the object reference changes. This non-live boundary approximates that
		// guard with object ids until KnownList target references are ported.
		if (playerObjectId <= 0)
			return PlayerTargetChangePacketPlan.BlockedInvalidPlayer(playerObjectId, currentTargetObjectId, newTarget);

		var newTargetObjectId = newTarget?.TargetObjectId ?? 0;
		if (currentTargetObjectId == newTargetObjectId)
			return PlayerTargetChangePacketPlan.NoChange(playerObjectId, currentTargetObjectId, newTarget);

		return PlayerTargetChangePacketPlan.Created(playerObjectId, currentTargetObjectId, newTarget);
	}
}

public sealed record PlayerTargetChangePacketPlan(
	PlayerTargetChangePacketPlanStatus Status,
	int PlayerObjectId,
	int PreviousTargetObjectId,
	int NewTargetObjectId,
	TargetSelectedSnapshot? NewTarget,
	SmTargetSelected? OwnerPacket,
	SmTargetUpdate? SightedPlayersPacket,
	string JavaSource)
{
	public bool ShouldUpdatePlayerTargetObjectId => Status == PlayerTargetChangePacketPlanStatus.PacketsCreated;

	public bool ShouldSendOwnerPacket => OwnerPacket is not null;

	public bool ShouldBroadcastToSightedPlayers => SightedPlayersPacket is not null;

	public static PlayerTargetChangePacketPlan Created(
		int playerObjectId,
		int previousTargetObjectId,
		TargetSelectedSnapshot? newTarget)
	{
		var newTargetObjectId = newTarget?.TargetObjectId ?? 0;
		return new PlayerTargetChangePacketPlan(
			PlayerTargetChangePacketPlanStatus.PacketsCreated,
			playerObjectId,
			previousTargetObjectId,
			newTargetObjectId,
			newTarget,
			new SmTargetSelected(newTarget),
			new SmTargetUpdate(playerObjectId, newTargetObjectId),
			"VisibleObject.setTarget -> PlayerController.onTargetChanged -> SM_TARGET_SELECTED + broadcast SM_TARGET_UPDATE");
	}

	public static PlayerTargetChangePacketPlan NoChange(
		int playerObjectId,
		int currentTargetObjectId,
		TargetSelectedSnapshot? newTarget)
	{
		return new PlayerTargetChangePacketPlan(
			PlayerTargetChangePacketPlanStatus.NoChange,
			playerObjectId,
			currentTargetObjectId,
			newTarget?.TargetObjectId ?? 0,
			newTarget,
			OwnerPacket: null,
			SightedPlayersPacket: null,
			"VisibleObject.setTarget reference guard prevented PlayerController.onTargetChanged");
	}

	public static PlayerTargetChangePacketPlan BlockedInvalidPlayer(
		int playerObjectId,
		int currentTargetObjectId,
		TargetSelectedSnapshot? newTarget)
	{
		return new PlayerTargetChangePacketPlan(
			PlayerTargetChangePacketPlanStatus.BlockedInvalidPlayer,
			playerObjectId,
			currentTargetObjectId,
			newTarget?.TargetObjectId ?? 0,
			newTarget,
			OwnerPacket: null,
			SightedPlayersPacket: null,
			"PlayerController.onTargetChanged requires a live player owner");
	}
}

public enum PlayerTargetChangePacketPlanStatus
{
	PacketsCreated,
	NoChange,
	BlockedInvalidPlayer,
}
