using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionSendEmblemData : GameServerPacket
{
	public const int PacketOpCode = 214;

	private readonly int _size;
	private readonly byte[] _data;

	public SmLegionSendEmblemData(int size, byte[] data)
		: base(PacketOpCode)
	{
		_size = Math.Max(0, size);
		_data = data;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_SEND_EMBLEM_DATA.writeImpl.
		buffer.WriteD(_size);
		buffer.WriteB(_data.AsSpan(0, Math.Min(_size, _data.Length)).ToArray());
	}
}
