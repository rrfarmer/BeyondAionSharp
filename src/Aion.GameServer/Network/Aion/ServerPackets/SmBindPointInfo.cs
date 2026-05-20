using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmBindPointInfo : GameServerPacket
{
	public const int PacketOpCode = 235;

	private readonly int _mapId;
	private readonly float _x;
	private readonly float _y;
	private readonly float _z;

	public SmBindPointInfo(int mapId, float x, float y, float z)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_BIND_POINT_INFO(int, float, float, float).
		_mapId = mapId;
		_x = x;
		_y = y;
		_z = z;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_BIND_POINT_INFO.writeImpl for obelisk bind point.
		buffer.WriteC(0);
		buffer.WriteC(1);
		buffer.WriteD(_mapId);
		buffer.WriteF(_x);
		buffer.WriteF(_y);
		buffer.WriteF(_z);
		buffer.WriteD(0);
	}
}
