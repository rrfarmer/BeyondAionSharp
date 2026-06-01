using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmGroupDataExchange : GameServerPacket
{
	public const int PacketOpCode = 178;

	private readonly byte[] _data;
	private readonly byte _action;
	private readonly byte _unknown2;

	private SmGroupDataExchange(byte[] data, byte action, byte unknown2)
		: base(PacketOpCode)
	{
		_data = data;
		_action = action;
		_unknown2 = unknown2;
	}

	public static SmGroupDataExchange NearbyBroadcast(byte[] data)
	{
		// Java parity: SM_GROUP_DATA_EXCHANGE(byte[] byteData) sets action to 1 and omits unk2.
		return new SmGroupDataExchange(Copy(data), 1, 0);
	}

	public static SmGroupDataExchange GroupBroadcast(byte[] data, byte action, byte unknown2)
	{
		// Java parity: SM_GROUP_DATA_EXCHANGE(byte[] byteData, int action, int unk2).
		return new SmGroupDataExchange(Copy(data), action, unknown2);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_action);
		if (_action != 1)
			buffer.WriteC(_unknown2);

		buffer.WriteD(_data.Length);
		buffer.WriteB(_data);
	}

	private static byte[] Copy(byte[] data)
	{
		return data.Length == 0 ? [] : data.ToArray();
	}
}
