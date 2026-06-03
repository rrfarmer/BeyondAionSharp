using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmWindstreamAnnounce : GameServerPacket
{
	public const int PacketOpCode = 164;

	private readonly int _flyPathId;
	private readonly int _mapId;
	private readonly int _streamId;
	private readonly int _state;

	public SmWindstreamAnnounce(int flyPathId, int mapId, int streamId, int state)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_WINDSTREAM_ANNOUNCE(int, int, int, int).
		_flyPathId = flyPathId;
		_mapId = mapId;
		_streamId = streamId;
		_state = state;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_WINDSTREAM_ANNOUNCE.writeImpl: writeD(bidirectional) writeD(mapId) writeD(streamId) writeC(state).
		buffer.WriteD(_flyPathId);
		buffer.WriteD(_mapId);
		buffer.WriteD(_streamId);
		buffer.WriteC(_state);
	}
}
