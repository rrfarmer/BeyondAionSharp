using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmNicknameCheckResponse : GameServerPacket
{
	public const int PacketOpCode = 233;

	public SmNicknameCheckResponse(int responseCode)
		: base(PacketOpCode)
	{
		ResponseCode = responseCode;
	}

	public int ResponseCode { get; }

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_NICKNAME_CHECK_RESPONSE.writeImpl.
		buffer.WriteC(ResponseCode);
	}
}
