using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmGroupLoot : GameClientPacket
{
	public CmGroupLoot(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_GROUP_LOOT.readImpl fields — only meaningful ones exposed.
	public int Index { get; private set; }
	public int ItemId { get; private set; }
	public int NpcObjectId { get; private set; }
	public byte DistributionMode { get; private set; } // 2=Roll, 3=Bid
	public int Roll { get; private set; } // 0=not rolled, 1=rolled
	public long Bid { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_GROUP_LOOT.readImpl.
		buffer.ReadD(); // groupId (unused)
		Index = buffer.ReadD();
		buffer.ReadD(); // unk1
		ItemId = buffer.ReadD();
		buffer.ReadC(); // unk2
		buffer.ReadC(); // unk3 (3.0)
		buffer.ReadC(); // unk4 (3.5)
		NpcObjectId = buffer.ReadD();
		DistributionMode = buffer.ReadC();
		Roll = buffer.ReadD();
		Bid = buffer.ReadQ();
	}
}
