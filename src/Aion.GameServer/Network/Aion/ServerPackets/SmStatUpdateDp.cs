using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmStatUpdateDp : GameServerPacket
{
	public const int PacketOpCode = 6;

	private readonly int _currentDp;

	public SmStatUpdateDp(int currentDp)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_STATUPDATE_DP.writeImpl.
		_currentDp = currentDp;
	}

	public int CurrentDp => _currentDp;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteH(_currentDp);
	}
}
