using System.Text;
using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Client;

public sealed class CmChannelRequest : AbstractClientPacket
{
	public CmChannelRequest(byte opCode)
		: base(opCode)
	{
	}

	public int ChannelRequestId { get; private set; }

	public byte[] ChannelIdentifierBytes { get; private set; } = [];

	public string ChannelIdentifier => Encoding.Unicode.GetString(ChannelIdentifierBytes);

	protected override void ReadPayload(PacketBuffer buffer)
	{
		buffer.ReadC();
		buffer.ReadH();
		ChannelRequestId = buffer.ReadD();
		buffer.ReadB(16);
		var identifierLength = buffer.ReadH() * 2;
		ChannelIdentifierBytes = buffer.ReadB(identifierLength);
		buffer.ReadD();
	}
}
