using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmFlyTime : GameServerPacket
{
	public const int PacketOpCode = 244;

	private readonly int _currentFp;
	private readonly int _maxFp;

	public SmFlyTime(int currentFp, int maxFp)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_FLY_TIME.writeImpl.
		_currentFp = currentFp;
		_maxFp = maxFp;
	}

	public int CurrentFp => _currentFp;

	public int MaxFp => _maxFp;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_currentFp);
		buffer.WriteD(_maxFp);
	}
}
