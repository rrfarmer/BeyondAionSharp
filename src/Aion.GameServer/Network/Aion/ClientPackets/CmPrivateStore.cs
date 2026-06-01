using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed record CmPrivateStoreEntry(
	int ItemObjectId,
	int ItemId,
	int Count,
	long Price);

public sealed class CmPrivateStore : GameClientPacket
{
	public CmPrivateStore(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ItemCount { get; private set; }

	public IReadOnlyList<CmPrivateStoreEntry> Items { get; private set; } = Array.Empty<CmPrivateStoreEntry>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_PRIVATE_STORE.readImpl.
		ItemCount = buffer.ReadH();

		var items = new List<CmPrivateStoreEntry>(ItemCount);
		for (var i = 0; i < ItemCount; i++)
		{
			items.Add(new CmPrivateStoreEntry(
				buffer.ReadD(),
				buffer.ReadD(),
				buffer.ReadH(),
				buffer.ReadQ()));
		}

		Items = items;
	}
}
