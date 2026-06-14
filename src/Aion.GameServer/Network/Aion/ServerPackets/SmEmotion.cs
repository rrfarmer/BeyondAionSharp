using Aion.Commons.Network;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmEmotion : GameServerPacket
{
	public const int PacketOpCode = 37;
	private readonly int _senderObjectId;
	private readonly EmotionType _emotionType;
	private readonly int _emotion;
	private readonly int _targetObjectId;
	private readonly float _speed;
	private readonly int _state;
	private readonly int _baseAttackSpeed;
	private readonly int _currentAttackSpeed;
	private readonly float _x;
	private readonly float _y;
	private readonly float _z;
	private readonly byte _heading;

	public SmEmotion(Player player, EmotionType emotionType)
		: this(player, emotionType, 0, 0)
	{
	}

	public SmEmotion(Creature creature, EmotionType emotionType)
		: this(creature, emotionType, 0, 0)
	{
		// Java parity: network/aion/serverpackets/SM_EMOTION(Creature, EmotionType).
	}

	public SmEmotion(Creature creature, EmotionType emotionType, int emotion, int targetObjectId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_EMOTION(Creature, EmotionType, int, int).
		_senderObjectId = creature.GetObjectId();
		_emotionType = emotionType;
		_emotion = emotion;
		_targetObjectId = targetObjectId;
		_state = creature.GetState();
		var aSpeed = creature.GetGameStats().GetAttackSpeed();
		_baseAttackSpeed = aSpeed.GetBase();
		_currentAttackSpeed = aSpeed.GetCurrent();
		_speed = creature.GetGameStats().GetMovementSpeedFloat();
	}

	public SmEmotion(
		int senderObjectId,
		EmotionType emotionType,
		int creatureState,
		float movementSpeed,
		int baseAttackSpeed,
		int currentAttackSpeed)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_EMOTION(Creature, EmotionType, int, int) for non-Player creatures.
		// Used for summon/NPC broadcasts where the creature's stats are snapshotted before packet construction.
		_senderObjectId = senderObjectId;
		_emotionType = emotionType;
		_state = creatureState;
		_speed = movementSpeed;
		_baseAttackSpeed = baseAttackSpeed;
		_currentAttackSpeed = currentAttackSpeed;
	}

	public SmEmotion(
		Player player,
		EmotionType emotionType,
		int emotion,
		int targetObjectId,
		float speed = 0,
		int baseAttackSpeed = 0,
		int currentAttackSpeed = 0)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_EMOTION(Creature, EmotionType, int, int).
		_senderObjectId = player.ObjectId;
		_emotionType = emotionType;
		_emotion = emotion;
		_targetObjectId = targetObjectId;
		_state = (int)player.CreatureState;
		_speed = speed;
		_baseAttackSpeed = baseAttackSpeed;
		_currentAttackSpeed = currentAttackSpeed;
	}

	public SmEmotion(
		Player player,
		EmotionType emotionType,
		int emotion,
		float x,
		float y,
		float z,
		byte heading,
		int targetObjectId,
		float speed = 0,
		int baseAttackSpeed = 0,
		int currentAttackSpeed = 0)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_EMOTION(Player, EmotionType, int, float, float, float, byte, int).
		_senderObjectId = player.ObjectId;
		_emotionType = emotionType;
		_emotion = emotion;
		_x = x;
		_y = y;
		_z = z;
		_heading = heading;
		_targetObjectId = targetObjectId;
		_state = (int)player.CreatureState;
		_speed = speed;
		_baseAttackSpeed = baseAttackSpeed;
		_currentAttackSpeed = currentAttackSpeed;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_EMOTION.writeImpl.
		buffer.WriteD(_senderObjectId);
		buffer.WriteC((int)_emotionType);
		buffer.WriteH(_state);
		buffer.WriteF(_speed);

		switch (_emotionType)
		{
			case EmotionType.LAND_FLYTELEPORT:
			case EmotionType.FLY:
			case EmotionType.LAND:
			case EmotionType.SELECT_TARGET:
			case EmotionType.JUMP:
			case EmotionType.SIT:
			case EmotionType.STAND:
			case EmotionType.ATTACKMODE_IN_MOVE:
			case EmotionType.NEUTRALMODE_IN_MOVE:
			case EmotionType.WALK:
			case EmotionType.RUN:
			case EmotionType.OPEN_PRIVATESHOP:
			case EmotionType.CLOSE_PRIVATESHOP:
			case EmotionType.POWERSHARD_ON:
			case EmotionType.POWERSHARD_OFF:
			case EmotionType.ATTACKMODE_IN_STANDING:
			case EmotionType.NEUTRALMODE_IN_STANDING:
			case EmotionType.START_FEEDING:
			case EmotionType.END_FEEDING:
			case EmotionType.WINDSTREAM_START_BOOST:
			case EmotionType.WINDSTREAM_END_BOOST:
			case EmotionType.WINDSTREAM_END:
			case EmotionType.WINDSTREAM_EXIT:
			case EmotionType.OPEN_DOOR:
			case EmotionType.CLOSE_DOOR:
			case EmotionType.WINDSTREAM_STRAFE:
			case EmotionType.STOP_GLIDE:
			case EmotionType.STOP_FLY:
				break;
			case EmotionType.DIE:
			case EmotionType.START_LOOT:
			case EmotionType.END_LOOT:
			case EmotionType.START_QUESTLOOT:
			case EmotionType.END_QUESTLOOT:
				buffer.WriteD(_targetObjectId);
				break;
			case EmotionType.CHAIR_SIT:
			case EmotionType.CHAIR_UP:
				buffer.WriteF(_x);
				buffer.WriteF(_y);
				buffer.WriteF(_z);
				buffer.WriteC(_heading);
				break;
			case EmotionType.START_FLYTELEPORT:
				buffer.WriteD(_emotion);
				break;
			case EmotionType.WINDSTREAM:
				buffer.WriteD(_emotion);
				buffer.WriteD(_targetObjectId);
				break;
			case EmotionType.RIDE:
			case EmotionType.RIDE_END:
				if (_targetObjectId != 0)
					buffer.WriteD(_targetObjectId);
				buffer.WriteF(0x3F);
				buffer.WriteF(0x3F);
				buffer.WriteF(0x40);
				break;
			case EmotionType.RESURRECT:
				buffer.WriteD(0);
				break;
			case EmotionType.EMOTE:
				buffer.WriteD(_targetObjectId);
				buffer.WriteH(_emotion);
				buffer.WriteC(1);
				break;
			case EmotionType.CHANGE_SPEED:
				buffer.WriteH(_baseAttackSpeed);
				buffer.WriteH(_currentAttackSpeed);
				buffer.WriteC(0);
				break;
			default:
				if (_targetObjectId != 0)
					buffer.WriteD(_targetObjectId);
				break;
		}
	}
}
