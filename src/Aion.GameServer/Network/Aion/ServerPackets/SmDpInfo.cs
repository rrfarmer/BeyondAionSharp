using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDpInfo : GameServerPacket
{
	public const int PacketOpCode = 7;

	private readonly int _playerObjectId;
	private readonly int _currentDp;

	public SmDpInfo(int playerObjectId, int currentDp)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_DP_INFO.writeImpl.
		_playerObjectId = playerObjectId;
		_currentDp = currentDp;
	}

	public int PlayerObjectId => _playerObjectId;

	public int CurrentDp => _currentDp;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_playerObjectId);
		buffer.WriteH(_currentDp);
	}
}
