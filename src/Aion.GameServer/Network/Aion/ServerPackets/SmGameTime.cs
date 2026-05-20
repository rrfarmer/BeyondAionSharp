using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmGameTime : GameServerPacket
{
	public const int PacketOpCode = 38;

	private readonly int _gameMinutes;

	public SmGameTime(int gameMinutes)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_GAME_TIME.writeImpl.
		_gameMinutes = gameMinutes;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_gameMinutes);
	}
}
