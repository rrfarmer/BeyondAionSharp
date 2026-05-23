using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmStatUpdateMp : GameServerPacket
{
	public const int PacketOpCode = 4;

	private readonly int _currentMp;
	private readonly int _maxMp;

	public SmStatUpdateMp(int currentMp, int maxMp)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_STATUPDATE_MP.writeImpl.
		_currentMp = currentMp;
		_maxMp = maxMp;
	}

	public int CurrentMp => _currentMp;

	public int MaxMp => _maxMp;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_currentMp);
		buffer.WriteD(_maxMp);
	}
}
