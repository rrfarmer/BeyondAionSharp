using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAllianceReadyCheck(int playerObjectId, int statusCode) : GameServerPacket(PacketOpCode)
{
	public const int PacketOpCode = 250;

	public int PlayerObjectId { get; } = playerObjectId;

	public int StatusCode { get; } = statusCode;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ALLIANCE_READY_CHECK.writeImpl.
		buffer.WriteD(PlayerObjectId);
		buffer.WriteC(StatusCode);
	}
}
