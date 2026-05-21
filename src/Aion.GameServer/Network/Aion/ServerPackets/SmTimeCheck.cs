using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmTimeCheck : GameServerPacket
{
	public const int PacketOpCode = 39;
	private readonly int _nanoTime;
	private readonly Func<int> _uptimeMillis;

	public SmTimeCheck(int nanoTime, Func<int>? uptimeMillis = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_TIME_CHECK(int nanoTime).
		_nanoTime = nanoTime;
		_uptimeMillis = uptimeMillis ?? (() => unchecked((int)Environment.TickCount64));
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_TIME_CHECK.writeImpl.
		buffer.WriteD(_uptimeMillis());
		buffer.WriteD(_nanoTime);
	}
}
