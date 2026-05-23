using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmStatUpdateHp : GameServerPacket
{
	public const int PacketOpCode = 3;

	private readonly int _currentHp;
	private readonly int _maxHp;

	public SmStatUpdateHp(int currentHp, int maxHp)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_STATUPDATE_HP.writeImpl.
		_currentHp = currentHp;
		_maxHp = maxHp;
	}

	public int CurrentHp => _currentHp;

	public int MaxHp => _maxHp;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_currentHp);
		buffer.WriteD(_maxHp);
	}
}
