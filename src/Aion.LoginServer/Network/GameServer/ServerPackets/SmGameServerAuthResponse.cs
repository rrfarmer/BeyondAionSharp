using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmGameServerAuthResponse : GsServerPacket
{
	private readonly GsAuthResponse _response;
	private readonly int _registeredServerCount;

	public SmGameServerAuthResponse(GsAuthResponse response, int registeredServerCount)
	{
		_response = response;
		_registeredServerCount = registeredServerCount;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(0);
		buffer.WriteC((byte)_response);
		if (_response == GsAuthResponse.AUTHED)
			buffer.WriteC(_registeredServerCount);
	}
}
