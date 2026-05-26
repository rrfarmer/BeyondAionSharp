using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public enum BindPointTeleportRuntimeFanoutStatus
{
	NoPacket,
	BroadcastVisiblePlayersAndSelf,
}

public sealed record BindPointTeleportRuntimeFanoutResult(
	BindPointTeleportRuntimeFanoutStatus Status,
	BindPointTeleportFanoutPlan? FanoutPlan,
	int SentCount,
	bool SentPacket,
	string JavaSource,
	bool IsLive);

public sealed class BindPointTeleportRuntimeFanoutService
{
	private readonly IGameClientConnectionRegistry _connectionRegistry;

	public BindPointTeleportRuntimeFanoutService(IGameClientConnectionRegistry connectionRegistry)
	{
		_connectionRegistry = connectionRegistry;
	}

	public async Task<BindPointTeleportRuntimeFanoutResult> BroadcastControlPlanAsync(
		BindPointTeleportRuntimeControlBridgePlan controlBridgePlan,
		WorldPosition sourcePosition,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!controlBridgePlan.ShouldSendPacket || controlBridgePlan.ControlPlan.Packet == null)
		{
			return new BindPointTeleportRuntimeFanoutResult(
				BindPointTeleportRuntimeFanoutStatus.NoPacket,
				FanoutPlan: null,
				SentCount: 0,
				SentPacket: false,
				controlBridgePlan.JavaSource,
				IsLive: false);
		}

		var source = controlBridgePlan.ControlPlan.Status == BindPointTeleportControlPlanStatus.BroadcastLoginCooldown
			? BindPointTeleportFanoutSource.LoginCooldownBroadcast
			: BindPointTeleportFanoutSource.CancelBroadcast;
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			source,
			controlBridgePlan.ControlPlan.PlayerObjectId,
			controlBridgePlan.ControlPlan.Packet);

		return await BroadcastFanoutPlanAsync(fanoutPlan, sourcePosition, cancellationToken);
	}

	public async Task<BindPointTeleportRuntimeFanoutResult> BroadcastFanoutPlanAsync(
		BindPointTeleportFanoutPlan fanoutPlan,
		WorldPosition sourcePosition,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		// Java parity: PacketSendUtility.broadcastPacket(player, packet, true) and broadcastPacketAndReceive
		// send to the source player before known-list players. The C# registry approximation uses includeSourcePlayer.
		var sentCount = await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			sourcePosition,
			fanoutPlan.SourcePlayerObjectId,
			fanoutPlan.Packet,
			includeSourcePlayer: fanoutPlan.IncludeSourcePlayer);
		return new BindPointTeleportRuntimeFanoutResult(
			BindPointTeleportRuntimeFanoutStatus.BroadcastVisiblePlayersAndSelf,
			fanoutPlan,
			sentCount,
			SentPacket: true,
			fanoutPlan.JavaSource,
			IsLive: true);
	}
}
