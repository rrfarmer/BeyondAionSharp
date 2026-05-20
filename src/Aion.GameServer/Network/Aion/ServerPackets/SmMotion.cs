using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMotion : GameServerPacket
{
	public const int PacketOpCode = 148;

	private readonly IReadOnlyList<PlayerMotion> _motions;
	private readonly Func<DateTimeOffset> _clock;

	public SmMotion(IReadOnlyList<PlayerMotion> motions, Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION(Collection<Motion>).
		_motions = motions;
		_clock = clock ?? (() => DateTimeOffset.Now);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION.writeImpl action 1.
		buffer.WriteC(1);
		buffer.WriteH(_motions.Count);
		var now = _clock();
		foreach (var motion in _motions)
		{
			buffer.WriteH(motion.Id);
			buffer.WriteD(motion.SecondsUntilExpiration(now));
			buffer.WriteC(motion.IsActive ? 1 : 0);
		}
	}
}
