using Aion.Commons.Network;

namespace Aion.LoginServer.Network.GameServer.ServerPackets;

public sealed class SmPremiumResponse : GsServerPacket
{
	private readonly int _requestId;
	private readonly int _result;
	private readonly long _points;

	public SmPremiumResponse(int requestId, int result, long points)
	{
		_requestId = requestId;
		_result = result;
		_points = points;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteC(10);
		buffer.WriteD(_requestId);
		buffer.WriteD(_result);
		buffer.WriteQ(_points);
	}
}
