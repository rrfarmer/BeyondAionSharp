using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using FaithfulMotion = Aion.GameServer.Model.GameObjects.Players.Motion.Motion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMotion : GameServerPacket
{
	public const int PacketOpCode = 148;

	private readonly IReadOnlyList<PlayerMotion> _motions;
	private readonly IReadOnlyDictionary<int, FaithfulMotion>? _activeMotions;
	private readonly Func<DateTimeOffset> _clock;
	private readonly byte _action;
	private readonly int _playerObjectId;
	private readonly int _motionId;
	private readonly byte _motionType;
	private readonly int _remainingTime;

	public SmMotion(IReadOnlyList<PlayerMotion> motions, Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION(Collection<Motion>).
		_motions = motions;
		_clock = clock ?? (() => DateTimeOffset.Now);
		_action = 1;
		_motionId = 0;
		_motionType = 0;
		_remainingTime = 0;
	}

	public SmMotion(int motionId, int remainingTime)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION(short motionId, int remainingTime).
		_motionId = motionId;
		_remainingTime = remainingTime;
		_motions = Array.Empty<PlayerMotion>();
		_clock = () => DateTimeOffset.Now;
		_action = 2;
		_motionType = 0;
	}

	public SmMotion(int motionId, byte motionType)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION(short motionId, byte type).
		_motionId = motionId;
		_motionType = motionType;
		_motions = Array.Empty<PlayerMotion>();
		_clock = () => DateTimeOffset.Now;
		_action = 5;
		_remainingTime = 0;
	}

	private SmMotion(int motionId, byte action, bool _)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION(short motionId) remove branch.
		_motionId = motionId;
		_motions = Array.Empty<PlayerMotion>();
		_clock = () => DateTimeOffset.Now;
		_action = action;
		_motionType = 0;
		_remainingTime = 0;
	}

	public SmMotion(short motionId)
		: this(motionId, action: 6, _: true)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION(short motionId) -> action 6 remove.
	}

	public SmMotion(int playerId, IDictionary<int, FaithfulMotion> activeMotions)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION(int playerId, Map<Integer,Motion> activeMotions).
		_playerObjectId = playerId;
		_activeMotions = new Dictionary<int, FaithfulMotion>(activeMotions);
		_motions = Array.Empty<PlayerMotion>();
		_clock = () => DateTimeOffset.Now;
		_action = 7;
		_motionId = 0;
		_motionType = 0;
		_remainingTime = 0;
	}

	public SmMotion(int playerObjectId, IReadOnlyList<PlayerMotion> motions, Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION(int playerId, activeMotions).
		_playerObjectId = playerObjectId;
		_motions = motions;
		_clock = clock ?? (() => DateTimeOffset.Now);
		_action = 7;
		_motionId = 0;
		_motionType = 0;
		_remainingTime = 0;
	}

	public static SmMotion Remove(int motionId)
	{
		return new SmMotion(motionId, action: 6, _: true);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MOTION.writeImpl.
		buffer.WriteC(_action);
		if (_action == 7)
		{
			buffer.WriteD(_playerObjectId);
			if (_activeMotions != null)
			{
				// Java parity: for (int i = 1; i < 6; i++) activeMotions.get(i).
				for (var i = 1; i < 6; i++)
					buffer.WriteH(_activeMotions.TryGetValue(i, out var motion) ? motion.GetId() : 0);
				return;
			}
			var activeMotions = _motions.Where(motion => motion.IsActive).ToArray();
			for (var motionType = 1; motionType <= 5; motionType++)
				buffer.WriteH(activeMotions.FirstOrDefault(motion => PlayerMotion.GetMotionType(motion.Id) == motionType)?.Id ?? 0);
			return;
		}

		if (_action == 5)
		{
			buffer.WriteH(_motionId);
			buffer.WriteC(_motionType);
			return;
		}

		if (_action == 6)
		{
			buffer.WriteH(_motionId);
			return;
		}

		if (_action == 2)
		{
			buffer.WriteH(_motionId);
			buffer.WriteD(_remainingTime);
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
