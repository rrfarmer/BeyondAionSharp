using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmPlayFail : AionServerPacket
{
	private readonly AionAuthResponse _response;

	public SmPlayFail(AionAuthResponse response)
		: base(0x06)
	{
		_response = response;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteD((int)_response);
	}
}
