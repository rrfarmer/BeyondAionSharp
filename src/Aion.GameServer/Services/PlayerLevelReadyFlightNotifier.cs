using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class PlayerLevelReadyFlightNotifier
{
	public static async Task<PlayerLevelReadyFlightNotification> NotifyIfFlyingAsync(
		Player player,
		IGameClientConnectionRegistry? connectionRegistry,
		PlayerVisualStatsUpdateService? playerVisualStats = null)
	{
		// Java parity: network/aion/clientpackets/CM_LEVEL_READY calls FlyController.startFly(true, true)
		// when the server-side fly-state survived teleport/loading.
		if (!player.IsInFlyingState())
			return new PlayerLevelReadyFlightNotification(false, null, 0, null);

		player.StartFlying();
		var visualStatsUpdate = playerVisualStats == null
			? null
			: await playerVisualStats.UpdateStatsAndSpeedVisuallyAsync(player, speedSnapshot: null);
		var packet = new SmEmotion(player, EmotionType.Fly);
		var broadcastCount = connectionRegistry == null
			? 0
			: await connectionRegistry.BroadcastToVisiblePlayersAsync(
				player.Position,
				player.ObjectId,
				packet,
				includeSourcePlayer: true);
		return new PlayerLevelReadyFlightNotification(true, packet, broadcastCount, visualStatsUpdate);
	}
}

public sealed record PlayerLevelReadyFlightNotification(
	bool WasFlying,
	SmEmotion? Packet,
	int BroadcastCount,
	PlayerVisualStatsUpdateResult? VisualStatsUpdate);
