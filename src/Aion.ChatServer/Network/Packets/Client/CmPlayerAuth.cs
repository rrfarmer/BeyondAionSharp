using System.Text;
using Aion.Commons.Network;

namespace Aion.ChatServer.Network.Packets.Client;

public sealed class CmPlayerAuth : AbstractClientPacket
{
	public CmPlayerAuth(byte opCode)
		: base(opCode)
	{
	}

	public string IdentifierSeparator { get; private set; } = string.Empty;

	public string GameName { get; private set; } = string.Empty;

	public int PlayerId { get; private set; }

	public byte[] Identifier { get; private set; } = [];

	public string AccountName { get; private set; } = string.Empty;

	public byte[] Token { get; private set; } = [];

	public string CharacterName
	{
		get
		{
			var nameIdentifier = Encoding.Unicode.GetString(Identifier);
			var separatorIndex = nameIdentifier.LastIndexOf(IdentifierSeparator, StringComparison.Ordinal);
			return separatorIndex < 0 ? nameIdentifier : nameIdentifier[..separatorIndex];
		}
	}

	protected override void ReadPayload(PacketBuffer buffer)
	{
		IdentifierSeparator = Encoding.Unicode.GetString(buffer.ReadB(2));
		buffer.ReadC();
		buffer.ReadD();

		var gameNameLength = buffer.ReadH() * 2;
		GameName = Encoding.Unicode.GetString(buffer.ReadB(gameNameLength));

		buffer.ReadD();
		buffer.ReadD();
		buffer.ReadD();
		PlayerId = buffer.ReadD();
		buffer.ReadD();
		buffer.ReadD();
		buffer.ReadD();

		var identifierLength = buffer.ReadH() * 2;
		Identifier = buffer.ReadB(identifierLength);

		var accountNameLength = buffer.ReadH() * 2;
		AccountName = Encoding.Unicode.GetString(buffer.ReadB(accountNameLength));

		var tokenLength = buffer.ReadH();
		Token = buffer.ReadB(tokenLength);
	}
}
