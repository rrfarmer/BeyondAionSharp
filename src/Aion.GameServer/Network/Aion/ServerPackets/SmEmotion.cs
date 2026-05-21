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
			case EmotionType.LandFlyTeleport:
			case EmotionType.Fly:
			case EmotionType.Land:
			case EmotionType.SelectTarget:
			case EmotionType.Jump:
			case EmotionType.Sit:
			case EmotionType.Stand:
			case EmotionType.AttackModeInMove:
			case EmotionType.NeutralModeInMove:
			case EmotionType.Walk:
			case EmotionType.Run:
			case EmotionType.OpenPrivateShop:
			case EmotionType.ClosePrivateShop:
			case EmotionType.PowershardOn:
			case EmotionType.PowershardOff:
			case EmotionType.AttackModeInStanding:
			case EmotionType.NeutralModeInStanding:
			case EmotionType.StartFeeding:
			case EmotionType.EndFeeding:
			case EmotionType.WindstreamStartBoost:
			case EmotionType.WindstreamEndBoost:
			case EmotionType.WindstreamEnd:
			case EmotionType.WindstreamExit:
			case EmotionType.OpenDoor:
			case EmotionType.CloseDoor:
			case EmotionType.WindstreamStrafe:
			case EmotionType.StopGlide:
			case EmotionType.StopFly:
				break;
			case EmotionType.Die:
			case EmotionType.StartLoot:
			case EmotionType.EndLoot:
			case EmotionType.StartQuestLoot:
			case EmotionType.EndQuestLoot:
				buffer.WriteD(_targetObjectId);
				break;
			case EmotionType.ChairSit:
			case EmotionType.ChairUp:
				buffer.WriteF(_x);
				buffer.WriteF(_y);
				buffer.WriteF(_z);
				buffer.WriteC(_heading);
				break;
			case EmotionType.StartFlyTeleport:
				buffer.WriteD(_emotion);
				break;
			case EmotionType.Windstream:
				buffer.WriteD(_emotion);
				buffer.WriteD(_targetObjectId);
				break;
			case EmotionType.Ride:
			case EmotionType.RideEnd:
				if (_targetObjectId != 0)
					buffer.WriteD(_targetObjectId);
				buffer.WriteF(0x3F);
				buffer.WriteF(0x3F);
				buffer.WriteF(0x40);
				break;
			case EmotionType.Resurrect:
				buffer.WriteD(0);
				break;
			case EmotionType.Emote:
				buffer.WriteD(_targetObjectId);
				buffer.WriteH(_emotion);
				buffer.WriteC(1);
				break;
			case EmotionType.ChangeSpeed:
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
