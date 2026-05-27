using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PlayerKiskLoginRestorePacketPlanService
{
	public static PlayerKiskLoginRestorePacketPlan CreatePlan(
		Player player,
		PlayerKiskOfflineBindingRestoreResult? restoredKiskBinding,
		WorldPosition? restoredKiskPosition,
		StaticData? staticData)
	{
		// Java parity: PlayerEnterWorldService -> KiskService.onLogin, sendObeliskBindPoint, sendKiskBindPoint.
		var directPackets = new List<GameServerPacket>();
		if (restoredKiskBinding?.Kisk != null)
			directPackets.Add(new SmKiskUpdate(restoredKiskBinding.Kisk));

		directPackets.Add(CreateObeliskBindPointPacket(player, staticData));

		if (restoredKiskBinding?.Kisk != null && restoredKiskPosition.HasValue)
			directPackets.Add(SmBindPointInfo.Kisk(restoredKiskPosition.Value, restoredKiskBinding.Kisk.ObjectId));

		return new PlayerKiskLoginRestorePacketPlan(
			directPackets,
			restoredKiskBinding?.Kisk,
			restoredKiskPosition,
			restoredKiskBinding?.AddedMember == true && restoredKiskPosition.HasValue);
	}

	private static SmBindPointInfo CreateObeliskBindPointPacket(Player player, StaticData? staticData)
	{
		// Java parity: services/teleport/TeleportService.sendObeliskBindPoint.
		if (player.BindPoint != null)
			return new SmBindPointInfo(player.BindPoint.MapId, player.BindPoint.X, player.BindPoint.Y, player.BindPoint.Z);

		var spawn = staticData?.PlayerInitialData.GetSpawnLocation(player.Race);
		return spawn == null
			? new SmBindPointInfo(player.Position.WorldId, player.Position.X, player.Position.Y, player.Position.Z)
			: new SmBindPointInfo(spawn.MapId, spawn.X, spawn.Y, spawn.Z);
	}
}

public sealed record PlayerKiskLoginRestorePacketPlan(
	IReadOnlyList<GameServerPacket> DirectPackets,
	PlayerKiskRuntimeState? RestoredKisk,
	WorldPosition? RestoredKiskPosition,
	bool ShouldBroadcastAddedMemberUpdate);
