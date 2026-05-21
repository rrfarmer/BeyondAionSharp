using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMotion : GameServerPacket
{
	public const int PacketOpCode = 148;

	private readonly IReadOnlyList<PlayerMotion> _motions;
	private readonly Func<DateTimeOffset> _clock;
	private readonly byte _action;
	private readonly int _playerObjectId;

	public SmMotion(IReadOnlyList<PlayerMotion> motions, Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION(Collection<Motion>).
		_motions = motions;
		_clock = clock ?? (() => DateTimeOffset.Now);
		_action = 1;
	}

	public SmMotion(int playerObjectId, IReadOnlyList<PlayerMotion> motions, Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION(int playerId, activeMotions).
		_playerObjectId = playerObjectId;
		_motions = motions;
		_clock = clock ?? (() => DateTimeOffset.Now);
		_action = 7;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION.writeImpl.
		buffer.WriteC(_action);
		if (_action == 7)
		{
			buffer.WriteD(_playerObjectId);
			var activeMotions = _motions.Where(motion => motion.IsActive).OrderBy(motion => motion.Id).Take(5).ToArray();
			for (var i = 0; i < 5; i++)
				buffer.WriteH(i < activeMotions.Length ? activeMotions[i].Id : 0);
			return;
		}

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
