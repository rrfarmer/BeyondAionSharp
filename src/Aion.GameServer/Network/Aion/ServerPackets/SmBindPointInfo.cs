using Aion.Commons.Network;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmBindPointInfo : GameServerPacket
{
	public const int PacketOpCode = 235;

	private readonly int _mapId;
	private readonly float _x;
	private readonly float _y;
	private readonly float _z;
	private readonly byte _bindPointType;
	private readonly int _kiskObjectId;

	public SmBindPointInfo(int mapId, float x, float y, float z)
		: this(bindPointType: 0, mapId, x, y, z, kiskObjectId: 0)
	{
		// Java parity: network/aion/serverpackets/SM_BIND_POINT_INFO(int, float, float, float).
	}

	private SmBindPointInfo(byte bindPointType, int mapId, float x, float y, float z, int kiskObjectId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_BIND_POINT_INFO.writeImpl.
		_bindPointType = bindPointType;
		_mapId = mapId;
		_x = x;
		_y = y;
		_z = z;
		_kiskObjectId = kiskObjectId;
	}

	public static SmBindPointInfo Kisk(WorldPosition position, int kiskObjectId)
	{
		// Java parity: network/aion/serverpackets/SM_BIND_POINT_INFO(Kisk).
		return new SmBindPointInfo(4, position.WorldId, position.X, position.Y, position.Z, kiskObjectId);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_BIND_POINT_INFO.writeImpl.
		buffer.WriteC(_bindPointType);
		buffer.WriteC(1);
		buffer.WriteD(_mapId);
		buffer.WriteF(_x);
		buffer.WriteF(_y);
		buffer.WriteF(_z);
		buffer.WriteD(_kiskObjectId);
	}
}
