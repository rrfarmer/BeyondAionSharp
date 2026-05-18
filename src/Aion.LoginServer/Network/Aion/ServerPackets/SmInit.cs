using Aion.Commons.Network;

namespace Aion.LoginServer.Network.Aion.ServerPackets;

public sealed class SmInit : AionServerPacket
{
	private readonly byte[] _publicRsaKey;
	private readonly byte[] _blowfishKey;
	private readonly int _sessionId;

	public SmInit(byte[] publicRsaKey, byte[] blowfishKey, int sessionId)
		: base(0x00)
	{
		if (publicRsaKey.Length != 128)
			throw new ArgumentException("SM_INIT RSA modulus must be 128 bytes.", nameof(publicRsaKey));
		if (blowfishKey.Length != 16)
			throw new ArgumentException("SM_INIT Blowfish key must be 16 bytes.", nameof(blowfishKey));

		_publicRsaKey = publicRsaKey;
		_blowfishKey = blowfishKey;
		_sessionId = sessionId;
	}

	protected override void WritePayload(PacketBuffer buffer)
	{
		buffer.WriteD(_sessionId);
		buffer.WriteD(0x0000C621);
		buffer.WriteB(_publicRsaKey);
		buffer.WriteB(new byte[16]);
		buffer.WriteB(_blowfishKey);
		buffer.WriteB(new byte[7]);
		buffer.WriteC(0);
		buffer.WriteD(0);
		buffer.WriteH(0);
		buffer.WriteC(0);
		buffer.WriteD(0x3FCE09ED);
		buffer.WriteD(0);
	}
}
