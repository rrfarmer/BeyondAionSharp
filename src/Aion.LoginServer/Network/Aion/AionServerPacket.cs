using Aion.Commons.Network;
using Aion.LoginServer.Network;
using Aion.LoginServer.Network.Crypto;

namespace Aion.LoginServer.Network.Aion;

public abstract class AionServerPacket
{
	protected AionServerPacket(byte opCode)
	{
		OpCode = opCode;
	}

	public byte OpCode { get; }

	public byte[] SerializePayload()
	{
		using var buffer = new PacketBuffer();
		buffer.WriteC(OpCode);
		WritePayload(buffer);
		return buffer.ToArray();
	}

	public byte[] SerializeUnencryptedFrame()
	{
		return PacketFrameCodec.CreateFrame(SerializePayload());
	}

	public byte[] SerializeEncryptedFrame(LoginCryptEngine cryptEngine)
	{
		var payload = SerializePayload();
		var encryptedPayload = new byte[payload.Length + 16];
		payload.CopyTo(encryptedPayload, 0);
		var encryptedLength = cryptEngine.Encrypt(encryptedPayload, 0, payload.Length);
		return PacketFrameCodec.CreateFrame(encryptedPayload.AsSpan(0, encryptedLength));
	}

	protected abstract void WritePayload(PacketBuffer buffer);
}
