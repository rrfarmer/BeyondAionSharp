using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmRestoreCharacter : GameServerPacket
{
	public const int PacketOpCode = 203;

	public SmRestoreCharacter(int characterObjectId, bool success)
		: base(PacketOpCode)
	{
		CharacterObjectId = characterObjectId;
		Success = success;
	}

	public int CharacterObjectId { get; }

	public bool Success { get; }

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_RESTORE_CHARACTER.writeImpl.
		buffer.WriteD(Success ? 0 : 0x10);
		buffer.WriteD(CharacterObjectId);
	}
}
