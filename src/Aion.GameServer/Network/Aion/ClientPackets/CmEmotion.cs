using Aion.Commons.Network;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmEmotion : GameClientPacket
{
	public CmEmotion(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public EmotionType EmotionType { get; private set; }

	public int Emotion { get; private set; }

	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public byte Heading { get; private set; }

	public int TargetObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_EMOTION.readImpl.
		EmotionType = EmotionTypes.FromId(buffer.ReadC());

		switch (EmotionType)
		{
			case EmotionType.WINDSTREAM_STRAFE:
				buffer.ReadC();
				break;
			case EmotionType.START_SPRINT:
				buffer.ReadD();
				break;
			case EmotionType.EMOTE:
				Emotion = buffer.ReadH();
				TargetObjectId = buffer.ReadD();
				break;
			case EmotionType.CHAIR_SIT:
			case EmotionType.CHAIR_UP:
				X = buffer.ReadF();
				Y = buffer.ReadF();
				Z = buffer.ReadF();
				Heading = buffer.ReadC();
				break;
		}
	}
}
