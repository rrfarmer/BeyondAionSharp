using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.GameServer;

public sealed class SmPlayerAuthResponse : GsServerPacket
{
	public SmPlayerAuthResponse(int playerId, byte[] token)
	{
		PlayerId = playerId;
		Token = token;
	}

	public int PlayerId { get; }

	public byte[] Token { get; }

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(GsPacketFactory.SmPlayerAuthResponse);
		buffer.WriteD(PlayerId);
		buffer.WriteC(Token.Length);
		buffer.WriteB(Token);
	}
}
