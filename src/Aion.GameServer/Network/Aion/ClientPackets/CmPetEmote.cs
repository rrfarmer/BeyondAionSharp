using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmPetEmote : GameClientPacket
{
	public CmPetEmote(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int EmoteId { get; private set; }

	public PetEmote Emote { get; private set; } = PetEmote.Unknown;

	public float X1 { get; private set; }

	public float Y1 { get; private set; }

	public float Z1 { get; private set; }

	public float X2 { get; private set; }

	public float Y2 { get; private set; }

	public float Z2 { get; private set; }

	public byte Heading { get; private set; }

	public int EmotionId { get; private set; }

	public int Unknown2 { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_PET_EMOTE.readImpl.
		EmoteId = buffer.ReadC();
		Emote = PetEmoteResolver.GetEmoteById(EmoteId);

		switch (Emote)
		{
			case PetEmote.MoveStop:
			case PetEmote.MovePositionUpdate:
				ReadCurrentPosition(buffer);
				break;
			case PetEmote.MoveTo:
				ReadCurrentPosition(buffer);
				X2 = buffer.ReadF();
				Y2 = buffer.ReadF();
				Z2 = buffer.ReadF();
				break;
			default:
				EmotionId = buffer.ReadC();
				Unknown2 = buffer.ReadC();
				break;
		}
	}

	private void ReadCurrentPosition(PacketBuffer buffer)
	{
		X1 = buffer.ReadF();
		Y1 = buffer.ReadF();
		Z1 = buffer.ReadF();
		Heading = buffer.ReadC();
	}
}
