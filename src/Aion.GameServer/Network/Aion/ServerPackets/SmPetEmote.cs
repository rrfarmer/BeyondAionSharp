using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed record SmPetEmoteSnapshot(
	int PetObjectId,
	PetEmote Emote,
	int EmotionId = 0,
	int Param1 = 0,
	float X = 0,
	float Y = 0,
	float Z = 0,
	byte Heading = 0,
	float TargetX = 0,
	float TargetY = 0,
	float TargetZ = 0);

public sealed class SmPetEmote : GameServerPacket
{
	public const int PacketOpCode = 187;

	private readonly SmPetEmoteSnapshot _snapshot;

	public SmPetEmote(SmPetEmoteSnapshot snapshot)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PET_EMOTE(Pet, PetEmote).
		_snapshot = snapshot;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_PET_EMOTE.writeImpl.
		buffer.WriteD(_snapshot.PetObjectId);
		buffer.WriteC((int)_snapshot.Emote);

		switch (_snapshot.Emote)
		{
			case PetEmote.MoveStop:
				buffer.WriteF(_snapshot.X);
				buffer.WriteF(_snapshot.Y);
				buffer.WriteF(_snapshot.Z);
				buffer.WriteC(_snapshot.Heading);
				break;
			case PetEmote.MoveTo:
				buffer.WriteF(_snapshot.X);
				buffer.WriteF(_snapshot.Y);
				buffer.WriteF(_snapshot.Z);
				buffer.WriteC(_snapshot.Heading);
				buffer.WriteF(_snapshot.TargetX);
				buffer.WriteF(_snapshot.TargetY);
				buffer.WriteF(_snapshot.TargetZ);
				break;
			default:
				buffer.WriteC(_snapshot.EmotionId);
				buffer.WriteC(_snapshot.Param1);
				break;
		}
	}
}
