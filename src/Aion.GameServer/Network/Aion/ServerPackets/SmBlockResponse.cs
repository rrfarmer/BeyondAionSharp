using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmBlockResponse : GameServerPacket
{
	public const int PacketOpCode = 223;
	public const byte BlockSuccessful = 0;
	public const byte UnblockSuccessful = 1;
	public const byte TargetNotFound = 2;
	public const byte ListFull = 3;
	public const byte CantBlockSelf = 4;
	public const byte EditNote = 5;

	private readonly byte _code;
	private readonly string _playerName;

	public SmBlockResponse(byte code, string playerName)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_BLOCK_RESPONSE(int code, String playerName).
		_code = code;
		_playerName = playerName;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_BLOCK_RESPONSE.writeImpl.
		buffer.WriteS(_playerName);
		buffer.WriteC(_code);
	}
}
