using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum BindPointTeleportFanoutSource
{
	TeleportStartBroadcast,
	TeleportCooldownBroadcast,
	CancelBroadcast,
	LoginCooldownBroadcast,
	CustomPvpStartBroadcast,
	CustomPvpCooldownBroadcast,
}

public enum BindPointTeleportFanoutPlanStatus
{
	BroadcastVisiblePlayersAndSelf,
}

public sealed record BindPointTeleportFanoutPlan(
	BindPointTeleportFanoutPlanStatus Status,
	BindPointTeleportFanoutSource Source,
	int SourcePlayerObjectId,
	GameServerPacket Packet,
	bool IncludeSourcePlayer,
	string JavaUtilityMethod,
	string CsharpRegistryMethod,
	string KnownListNote,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportFanoutPlanService
{
	public static BindPointTeleportFanoutPlan CreatePlan(
		BindPointTeleportFanoutSource source,
		int sourcePlayerObjectId,
		GameServerPacket packet)
	{
		// Java parity: utils/PacketSendUtility.broadcastPacket(player, packet, true)
		// and broadcastPacketAndReceive(player, packet) both send to the source player and then known players.
		var javaUtilityMethod = source == BindPointTeleportFanoutSource.LoginCooldownBroadcast
			? "PacketSendUtility.broadcastPacketAndReceive(player, packet)"
			: "PacketSendUtility.broadcastPacket(player, packet, true)";

		return new BindPointTeleportFanoutPlan(
			BindPointTeleportFanoutPlanStatus.BroadcastVisiblePlayersAndSelf,
			source,
			sourcePlayerObjectId,
			packet,
			IncludeSourcePlayer: true,
			javaUtilityMethod,
			"IGameClientConnectionRegistry.BroadcastToVisiblePlayersAsync(..., includeSourcePlayer: true)",
			"C# visible-distance fanout approximates Java KnownList.forEachPlayer until persistent known-list membership is ported.",
			CreateJavaSource(source),
			IsLive: false);
	}

	private static string CreateJavaSource(BindPointTeleportFanoutSource source)
	{
		return source switch
		{
			BindPointTeleportFanoutSource.LoginCooldownBroadcast =>
				"BindPointTeleportService.onLogin -> PacketSendUtility.broadcastPacketAndReceive(player, SM_BIND_POINT_TELEPORT(action=3))",
			BindPointTeleportFanoutSource.TeleportStartBroadcast =>
				"BindPointTeleportService.teleport -> PacketSendUtility.broadcastPacket(player, SM_BIND_POINT_TELEPORT(action=1), true)",
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast =>
				"BindPointTeleportService.teleport scheduled task -> PacketSendUtility.broadcastPacket(player, SM_BIND_POINT_TELEPORT(action=3), true)",
			BindPointTeleportFanoutSource.CancelBroadcast =>
				"BindPointTeleportService.cancelTeleport -> PacketSendUtility.broadcastPacket(player, SM_BIND_POINT_TELEPORT(action=2), true)",
			BindPointTeleportFanoutSource.CustomPvpStartBroadcast =>
				"PvpMapHandler -> PacketSendUtility.broadcastPacket(player, SM_BIND_POINT_TELEPORT(action=1), true)",
			BindPointTeleportFanoutSource.CustomPvpCooldownBroadcast =>
				"PvpMapHandler -> PacketSendUtility.broadcastPacket(player, SM_BIND_POINT_TELEPORT(action=3), true)",
			_ => "PacketSendUtility bind-point broadcast",
		};
	}
}
