using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ClientPackets;

public sealed class CmLogin : AionClientPacket
{
	public CmLogin(byte opCode)
		: base(opCode)
	{
	}

	public byte[] EncryptedLoginData { get; private set; } = Array.Empty<byte>();

	public int SessionId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		var encryptedLength = buffer.Remaining - 55;
		if (encryptedLength < 0 || encryptedLength % 128 != 0)
			throw new InvalidOperationException($"Invalid CM_LOGIN encrypted credential length: {encryptedLength}.");

		EncryptedLoginData = buffer.ReadB(encryptedLength);
		SessionId = buffer.ReadD();
		buffer.ReadB(16);
		buffer.ReadB(7);
		buffer.ReadB(16);
		buffer.ReadD();
		buffer.ReadD();
		buffer.ReadD();
	}
}
