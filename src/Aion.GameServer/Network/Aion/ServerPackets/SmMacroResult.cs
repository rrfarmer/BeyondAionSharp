using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMacroResult : GameServerPacket
{
	public const int PacketOpCode = 232;
	public static readonly SmMacroResult Created = new(0);
	public static readonly SmMacroResult Deleted = new(1);

	private readonly int _code;

	private SmMacroResult(int code)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MACRO_RESULT(int).
		_code = code;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MACRO_RESULT.writeImpl.
		buffer.WriteC(_code);
	}
}
