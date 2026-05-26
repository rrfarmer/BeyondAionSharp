using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed record SmPetEmoteSnapshot(
	int PetObjectId,
	PetEmote Emote,
	int EmotionId = 0,
	int Param1 = 0);

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
		// Java parity: SM_PET_EMOTE.writeImpl default branch; MOVE_STOP/MOVETO are not ported in this slice.
		buffer.WriteD(_snapshot.PetObjectId);
		buffer.WriteC((int)_snapshot.Emote);

		switch (_snapshot.Emote)
		{
			case PetEmote.MoveStop:
			case PetEmote.MoveTo:
				throw new NotSupportedException($"SM_PET_EMOTE movement branch {_snapshot.Emote} is not ported in the known-list serializer subset.");
			default:
				buffer.WriteC(_snapshot.EmotionId);
				buffer.WriteC(_snapshot.Param1);
				break;
		}
	}
}
