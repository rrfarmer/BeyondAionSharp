using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public sealed class ItemPurificationPacketSendAdapter
{
	private readonly IGameClientConnectionRegistry? _connectionRegistry;

	public ItemPurificationPacketSendAdapter(IGameClientConnectionRegistry? connectionRegistry = null)
	{
		_connectionRegistry = connectionRegistry;
	}

	public async ValueTask<ItemPurificationPacketSendResult> SendConcretePacketsAsync(
		int playerObjectId,
		ItemPurificationPacketPlan? packetPlan,
		CancellationToken cancellationToken = default)
	{
		if (packetPlan == null)
			return ItemPurificationPacketSendResult.Failed(ItemPurificationPacketSendStatus.MissingPacketPlan);
		if (!packetPlan.Succeeded)
			return ItemPurificationPacketSendResult.Failed(ItemPurificationPacketSendStatus.PacketPlanNotReady);
		if (packetPlan.Operations.Count == 0)
			return ItemPurificationPacketSendResult.Failed(ItemPurificationPacketSendStatus.NoOperations);

		var concreteOperations = packetPlan.Operations
			.Where(operation => operation.ConcretePacket != null)
			.ToArray();
		var skippedMetadataOperations = packetPlan.Operations
			.Where(operation => operation.ConcretePacket == null)
			.ToArray();
		var packets = concreteOperations
			.Select(operation => operation.ConcretePacket!)
			.ToArray();
		var sentCount = 0;

		if (_connectionRegistry != null)
		{
			foreach (var packet in packets)
			{
				cancellationToken.ThrowIfCancellationRequested();
				// Java parity: this is only the PacketSendUtility.sendPacket boundary.
				// Runtime mutation, cube-size/AP packet construction, persistence, and quest hooks stay out of scope.
				if (await _connectionRegistry.SendPacketToPlayerAsync(playerObjectId, packet))
					sentCount++;
			}
		}

		return new ItemPurificationPacketSendResult(
			ItemPurificationPacketSendStatus.Ready,
			packetPlan.Status,
			concreteOperations,
			packets,
			skippedMetadataOperations,
			sentCount);
	}
}

public sealed record ItemPurificationPacketSendResult(
	ItemPurificationPacketSendStatus Status,
	ItemPurificationPacketPlanStatus? PacketPlanStatus,
	IReadOnlyList<ItemPurificationPacketOperation> ConcreteOperations,
	IReadOnlyList<GameServerPacket> Packets,
	IReadOnlyList<ItemPurificationPacketOperation> SkippedMetadataOperations,
	int SentCount)
{
	public bool Succeeded => Status == ItemPurificationPacketSendStatus.Ready;

	public static ItemPurificationPacketSendResult Failed(ItemPurificationPacketSendStatus status)
	{
		return new ItemPurificationPacketSendResult(
			status,
			null,
			Array.Empty<ItemPurificationPacketOperation>(),
			Array.Empty<GameServerPacket>(),
			Array.Empty<ItemPurificationPacketOperation>(),
			SentCount: 0);
	}
}

public enum ItemPurificationPacketSendStatus
{
	Ready,
	MissingPacketPlan,
	PacketPlanNotReady,
	NoOperations,
}
