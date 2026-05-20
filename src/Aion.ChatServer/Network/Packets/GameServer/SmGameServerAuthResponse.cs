using Aion.Commons.Network;
using Aion.ChatServer.Configuration;
using Aion.ChatServer.Services;

namespace Aion.ChatServer.Network.Packets.GameServer;

public sealed class SmGameServerAuthResponse : GsServerPacket
{
	private readonly ChatServerOptions _options;

	public SmGameServerAuthResponse(GsAuthResponse response, ChatServerOptions options)
	{
		Response = response;
		_options = options;
	}

	public GsAuthResponse Response { get; }

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(GsPacketFactory.SmGameServerAuthResponse);
		buffer.WriteC((byte)Response);
		if (Response != GsAuthResponse.Authed)
			return;

		var addressBytes = _options.ClientConnectEndPoint.Address.GetAddressBytes();
		buffer.WriteC(addressBytes.Length);
		buffer.WriteB(addressBytes);
		buffer.WriteH(_options.ClientConnectEndPoint.Port);
	}
}
