using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahInventorySendAdapterStatus
{
	NoPacketIntent,
	DisabledNoSend,
	MissingConnection,
	Sent,
	Failed,
}

public sealed record BindPointTeleportKinahInventorySendAdapterPlan(
	BindPointTeleportKinahInventorySendAdapterStatus Status,
	BindPointTeleportKinahInventoryUpdatePacketPlan PacketPlan,
	BindPointTeleportKinahInventorySendResult SendResult,
	bool WouldCallSendPacketAsync,
	bool DidCallSendPacketAsync,
	string JavaSource,
	bool IsLive
);

public static class BindPointTeleportKinahInventorySendAdapterPlanService
{
	public static BindPointTeleportKinahInventorySendAdapterPlan CreateDisabledPlan(
		BindPointTeleportKinahInventoryUpdatePacketPlan packetPlan,
		int playerObjectId,
		IGameClientConnectionRegistry? connectionRegistry = null
	)
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
				IsLive: false
			);

			return new BindPointTeleportKinahInventorySendAdapterPlan(
				BindPointTeleportKinahInventorySendAdapterStatus.NoPacketIntent,
				packetPlan,
				sendResult,
				WouldCallSendPacketAsync: false,
				DidCallSendPacketAsync: false,
				"PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM) boundary skipped because packet intent is absent",
				IsLive: false
			);
		}

		var disabledSendResult = new BindPointTeleportKinahInventorySendResult(
			BindPointTeleportKinahInventorySendStatus.Failed,
			playerObjectId,
			SentPacket: false,
			"Scheduled bind-point Kinah inventory update send adapter is disabled; SendPacketAsync was not called",
			IsLive: false
		);

		return new BindPointTeleportKinahInventorySendAdapterPlan(
			BindPointTeleportKinahInventorySendAdapterStatus.DisabledNoSend,
			packetPlan,
			disabledSendResult,
			WouldCallSendPacketAsync: true,
			DidCallSendPacketAsync: false,
			"PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM) boundary identified, but live C# SendPacketAsync remains disabled",
			IsLive: false
		);
	}
}

public sealed class BindPointTeleportKinahInventorySendAdapterService
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly bool _enabled;

	public BindPointTeleportKinahInventorySendAdapterService(IGameClientConnectionRegistry? connectionRegistry = null, bool enabled = false)
	{
		_connectionRegistry = connectionRegistry;
		_enabled = enabled;
	}

	public async Task<BindPointTeleportKinahInventorySendAdapterPlan> ExecuteAsync(
		BindPointTeleportKinahInventoryUpdatePacketPlan packetPlan,
		int playerObjectId,
		CancellationToken cancellationToken = default
	)
	{
		// Java parity: ItemPacketService.sendItemUpdatePacket sends
		// SM_INVENTORY_UPDATE_ITEM before bind-point cooldown fanout. This C# adapter keeps
		// the SendPacketAsync boundary opt-in and isolated from GameServerConnection dispatch.
		if (!packetPlan.ShouldSendPacket || packetPlan.Packet == null)
			return BindPointTeleportKinahInventorySendAdapterPlanService.CreateDisabledPlan(packetPlan, playerObjectId);

		if (!_enabled)
			return BindPointTeleportKinahInventorySendAdapterPlanService.CreateDisabledPlan(packetPlan, playerObjectId, _connectionRegistry);

		if (_connectionRegistry == null)
		{
			var missingConnectionResult = new BindPointTeleportKinahInventorySendResult(
				BindPointTeleportKinahInventorySendStatus.MissingConnection,
				playerObjectId,
				SentPacket: false,
				"Scheduled bind-point Kinah inventory update send adapter was enabled, but no connection registry was available",
				IsLive: true
			);

			return new BindPointTeleportKinahInventorySendAdapterPlan(
				BindPointTeleportKinahInventorySendAdapterStatus.MissingConnection,
				packetPlan,
				missingConnectionResult,
				WouldCallSendPacketAsync: true,
				DidCallSendPacketAsync: false,
				"PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM) could not run because the player connection registry was missing",
				IsLive: true
			);
		}

		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var sent = await _connectionRegistry.SendPacketToPlayerAsync(playerObjectId, packetPlan.Packet);
			var sendResult = new BindPointTeleportKinahInventorySendResult(
				sent ? BindPointTeleportKinahInventorySendStatus.Sent : BindPointTeleportKinahInventorySendStatus.MissingConnection,
				playerObjectId,
				SentPacket: sent,
				sent
					? "PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM) executed through the opt-in C# connection registry"
					: "Scheduled bind-point Kinah inventory update packet was not sent because the player connection was missing",
				IsLive: true
			);

			return new BindPointTeleportKinahInventorySendAdapterPlan(
				sent ? BindPointTeleportKinahInventorySendAdapterStatus.Sent : BindPointTeleportKinahInventorySendAdapterStatus.MissingConnection,
				packetPlan,
				sendResult,
				WouldCallSendPacketAsync: true,
				DidCallSendPacketAsync: true,
				sendResult.JavaSource,
				IsLive: true
			);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			var failedResult = new BindPointTeleportKinahInventorySendResult(
				BindPointTeleportKinahInventorySendStatus.Failed,
				playerObjectId,
				SentPacket: false,
				"Scheduled bind-point Kinah inventory update SendPacketAsync call failed; cooldown/action 3 fanout must remain blocked",
				IsLive: true
			);

			return new BindPointTeleportKinahInventorySendAdapterPlan(
				BindPointTeleportKinahInventorySendAdapterStatus.Failed,
				packetPlan,
				failedResult,
				WouldCallSendPacketAsync: true,
				DidCallSendPacketAsync: true,
				"PacketSendUtility.sendPacket(player, SM_INVENTORY_UPDATE_ITEM) threw at the opt-in C# send boundary",
				IsLive: true
			);
		}
	}
}
