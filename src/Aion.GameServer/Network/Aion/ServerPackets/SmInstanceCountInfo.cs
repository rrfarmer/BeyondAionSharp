using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmInstanceCountInfo : GameServerPacket
{
	public const int PacketOpCode = 147;

	private readonly int _mapId;
	private readonly int _instanceId;

	public SmInstanceCountInfo(int mapId, int instanceId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_INSTANCE_COUNT_INFO(int, int).
		_mapId = mapId;
		_instanceId = instanceId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_INSTANCE_COUNT_INFO.writeImpl: writeD(mapId) writeD(instanceId) writeD(1).
		// The trailing 1 is hardcoded solo flag (group=31, alliance=61 per Java comment).
		buffer.WriteD(_mapId);
		buffer.WriteD(_instanceId);
		buffer.WriteD(1);
	}
}
