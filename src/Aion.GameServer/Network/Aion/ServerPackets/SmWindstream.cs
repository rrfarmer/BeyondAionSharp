using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmWindstream : GameServerPacket
{
	public const int PacketOpCode = 163;

	private readonly int _state;
	private readonly int _result;

	public SmWindstream(int state, int result)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_WINDSTREAM(int, int).
		_state = state;
		_result = result;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_WINDSTREAM.writeImpl: writeD(unk1), writeC(unk2).
		buffer.WriteD(_state);
		buffer.WriteC(_result);
	}
}
