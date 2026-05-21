using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmFriendNotify : GameServerPacket
{
	public const int PacketOpCode = 225;
	public const byte Login = 0;
	public const byte Logout = 1;
	public const byte Deleted = 2;

	private readonly byte _code;
	private readonly string _name;

	public SmFriendNotify(byte code, string name)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_FRIEND_NOTIFY(byte code, String name).
		_code = code;
		_name = name;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_FRIEND_NOTIFY.writeImpl.
		buffer.WriteS(_name);
		buffer.WriteC(_code);
	}
}
