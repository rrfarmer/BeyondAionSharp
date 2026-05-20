using System.Buffers.Binary;
using System.Text;

namespace Aion.GameServer.Network.Aion;

internal sealed class GameEncryptionKeyPair
{
	private const byte StaticClientPacketCode = 0x65;
	private static readonly byte[] StaticKey = Encoding.ASCII.GetBytes("nKO/WctQ0AVLbpzfBkS6NevDYT8ourG5CRlmdjyJ72aswx4EPq1UgZhFMXH?3iI9");

	private readonly byte[] _serverKey;
	private readonly byte[] _clientKey;

	public GameEncryptionKeyPair(int baseKey)
	{
		_serverKey =
		[
			(byte)(baseKey & 0xff),
			(byte)((baseKey >> 8) & 0xff),
			(byte)((baseKey >> 16) & 0xff),
			(byte)((baseKey >> 24) & 0xff),
			0xa1,
			0x6c,
			0x54,
			0x87,
		];
		_clientKey = (byte[])_serverKey.Clone();
	}

	public void EncryptServer(Span<byte> data)
	{
		if (data.Length == 0)
			return;

		data[0] ^= _serverKey[0];
		var previous = data[0];

		for (var i = 1; i < data.Length; i++)
		{
			data[i] ^= (byte)(StaticKey[i & 63] ^ _serverKey[i & 7] ^ previous);
			previous = data[i];
		}

		UpdateKey(_serverKey, data.Length);
	}

	public bool DecryptClient(Span<byte> data)
	{
		if (data.Length == 0)
			return false;

		var previous = data[0];
		data[0] ^= _clientKey[0];

		for (var i = 1; i < data.Length; i++)
		{
			var current = data[i];
			data[i] ^= (byte)(StaticKey[i & 63] ^ _clientKey[i & 7] ^ previous);
			previous = current;
		}

		if (!ValidateClientPacket(data))
			return false;

		UpdateKey(_clientKey, data.Length);
		return true;
	}

	private static bool ValidateClientPacket(ReadOnlySpan<byte> data)
	{
		if (data.Length < 5 || data[2] != StaticClientPacketCode)
			return false;

		var opcode = BinaryPrimitives.ReadUInt16LittleEndian(data[..2]);
		var flippedOpcode = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(3, 2));
		return (ushort)~opcode == flippedOpcode;
	}

	private static void UpdateKey(byte[] key, int packetSize)
	{
		var oldKey = BinaryPrimitives.ReadUInt64LittleEndian(key);
		oldKey += (uint)packetSize;
		BinaryPrimitives.WriteUInt64LittleEndian(key, oldKey);
	}
}
