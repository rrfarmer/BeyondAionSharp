using System.Text;
using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Client;

public sealed class CmChannelCreate : AbstractClientPacket
{
	public CmChannelCreate(byte opCode)
		: base(opCode)
	{
	}

	public int ChannelRequestId { get; private set; }

	public byte[] ChannelIdentifierBytes { get; private set; } = [];

	public string ChannelIdentifier => Encoding.Unicode.GetString(ChannelIdentifierBytes);

	public byte[] PasswordBytes { get; private set; } = [];

	public string Password => Encoding.Unicode.GetString(PasswordBytes);

	public ushort FinalMarker { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		buffer.ReadC();
		buffer.ReadH();
		ChannelRequestId = buffer.ReadD();
		buffer.ReadB(16);
		var identifierLength = buffer.ReadH() * 2;
		ChannelIdentifierBytes = buffer.ReadB(identifierLength);
		buffer.ReadB(7);
		var passwordLength = buffer.ReadH() * 2;
		PasswordBytes = buffer.ReadB(passwordLength);
		FinalMarker = buffer.ReadH();
	}
}
