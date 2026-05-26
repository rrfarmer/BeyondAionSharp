using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahInventorySendAdapterStatus
{
	NoPacketIntent,
	DisabledNoSend,
}

public sealed record BindPointTeleportKinahInventorySendAdapterPlan(
	BindPointTeleportKinahInventorySendAdapterStatus Status,
	BindPointTeleportKinahInventoryUpdatePacketPlan PacketPlan,
	BindPointTeleportKinahInventorySendResult SendResult,
	bool WouldCallSendPacketAsync,
	bool DidCallSendPacketAsync,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKinahInventorySendAdapterPlanService
{
	public static BindPointTeleportKinahInventorySendAdapterPlan CreateDisabledPlan(
		BindPointTeleportKinahInventoryUpdatePacketPlan packetPlan,
		int playerObjectId,
		IGameClientConnectionRegistry? connectionRegistry = null)
	{
		// Java parity: PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM) is the live boundary.
		// This disabled seam records the boundary without calling SendPacketAsync.
		_ = connectionRegistry;

		if (!packetPlan.ShouldSendPacket || packetPlan.Packet == null)
		{
			var sendResult = new BindPointTeleportKinahInventorySendResult(
				BindPointTeleportKinahInventorySendStatus.Failed,
				playerObjectId,
				SentPacket: false,
				"Scheduled bind-point Kinah inventory update send adapter found no packet intent; live send remains disabled",
				IsLive: false);

			return new BindPointTeleportKinahInventorySendAdapterPlan(
				BindPointTeleportKinahInventorySendAdapterStatus.NoPacketIntent,
				packetPlan,
				sendResult,
				WouldCallSendPacketAsync: false,
				DidCallSendPacketAsync: false,
				"PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM) boundary skipped because packet intent is absent",
				IsLive: false);
		}

		var disabledSendResult = new BindPointTeleportKinahInventorySendResult(
			BindPointTeleportKinahInventorySendStatus.Failed,
			playerObjectId,
			SentPacket: false,
			"Scheduled bind-point Kinah inventory update send adapter is disabled; SendPacketAsync was not called",
			IsLive: false);

		return new BindPointTeleportKinahInventorySendAdapterPlan(
			BindPointTeleportKinahInventorySendAdapterStatus.DisabledNoSend,
			packetPlan,
			disabledSendResult,
			WouldCallSendPacketAsync: true,
			DidCallSendPacketAsync: false,
			"PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM) boundary identified, but live C# SendPacketAsync remains disabled",
			IsLive: false);
	}
}
