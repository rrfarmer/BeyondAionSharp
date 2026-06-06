using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionLeaveMember : GameServerPacket
{
	public const int PacketOpCode = 112; // Java parity: ServerPacketsOpcodes addPacketOpcode(112, SM_LEGION_LEAVE_MEMBER.class).

	private readonly int _messageId;
	private readonly int _playerObjectId;
	private readonly string _name;
	private readonly string _name1;

	public SmLegionLeaveMember(int messageId, int playerObjectId, string name, string? name1 = null)
		: base(PacketOpCode)
	{
		_messageId = messageId;
		_playerObjectId = playerObjectId;
		_name = name;
		_name1 = name1 ?? string.Empty;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_LEAVE_MEMBER.writeImpl.
		buffer.WriteD(_playerObjectId);
		buffer.WriteC(0);
		buffer.WriteD(0);
		buffer.WriteD(_messageId);
		buffer.WriteS(_name);
		buffer.WriteS(_name1);
	}
}
