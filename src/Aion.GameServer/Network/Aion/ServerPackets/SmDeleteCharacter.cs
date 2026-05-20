using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDeleteCharacter : GameServerPacket
{
	public const int PacketOpCode = 202;

	public SmDeleteCharacter(int playerObjectId, int deletionTimeSeconds)
		: base(PacketOpCode)
	{
		PlayerObjectId = playerObjectId;
		DeletionTimeSeconds = deletionTimeSeconds;
	}

	public int PlayerObjectId { get; }

	public int DeletionTimeSeconds { get; }

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_DELETE_CHARACTER.writeImpl.
		if (PlayerObjectId != 0)
		{
			buffer.WriteD(0);
			buffer.WriteD(PlayerObjectId);
			buffer.WriteD(DeletionTimeSeconds);
			return;
		}

		buffer.WriteD(0x10);
		buffer.WriteD(0);
		buffer.WriteD(0);
	}
}
