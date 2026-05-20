using System.Text;
using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Client;

public sealed class CmChannelJoin : AbstractClientPacket
{
	public CmChannelJoin(byte opCode)
		: base(opCode)
	{
	}

	public int ChannelRequestId { get; private set; }

	public byte[] ChannelIdentifierBytes { get; private set; } = [];

	public string ChannelIdentifier => Encoding.Unicode.GetString(ChannelIdentifierBytes);

	public byte[] PasswordBytes { get; private set; } = [];

	public string Password => Encoding.Unicode.GetString(PasswordBytes);

	protected override void ReadPayload(PacketBuffer buffer)
	{
		buffer.ReadC();
		buffer.ReadH();
		ChannelRequestId = buffer.ReadD();
		buffer.ReadB(16);
		var identifierLength = buffer.ReadH() * 2;
		ChannelIdentifierBytes = buffer.ReadB(identifierLength);
		var passwordLength = buffer.ReadH() * 2;
		PasswordBytes = buffer.ReadB(passwordLength);
	}
}
