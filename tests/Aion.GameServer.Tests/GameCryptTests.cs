using System.Buffers.Binary;
using System.Text;
using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class GameCryptTests
{
	[Fact]
	public void SerializeFrame_LeavesSmKeyUnencryptedThenEncryptsNextServerPacket()
	{
		var crypt = new GameCrypt(() => 0x01020304);
		var smKeyFrame = new SmKey().SerializeFrame(crypt);
		var expectedSmKeyFrame = CreateServerFrame(72, writer => writer.WriteD(GetEncodedKey(0x01020304)));

		Assert.Equal(expectedSmKeyFrame, smKeyFrame);
		Assert.True(crypt.IsEnabled);

		var smPongFrame = new SmPong().SerializeFrame(crypt);
		var expectedSmPongFrame = CreateServerFrame(142, writer =>
		{
			writer.WriteC(0);
			writer.WriteC(0);
		});
		var encryptor = new ServerPayloadEncryptor(0x01020304);
		encryptor.Encrypt(expectedSmPongFrame.AsSpan(2));

		Assert.Equal(expectedSmPongFrame, smPongFrame);
	}

	[Fact]
	public void DecryptClientPayload_AdvancesClientKeyAfterValidPacket()
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var encryptor = new ClientPayloadEncryptor(0x01020304);
		var first = encryptor.Encrypt(CreateClientPayload(37, 5001, 0));
		var second = encryptor.Encrypt(CreateClientPayload(236, 5001, 0, 1));

		Assert.True(crypt.DecryptClientPayload(first));
		Assert.True(crypt.DecryptClientPayload(second));
		Assert.Equal(CreateClientPayload(37, 5001, 0), first);
		Assert.Equal(CreateClientPayload(236, 5001, 0, 1), second);
	}

	[Fact]
	public void DecryptClientPayload_DoesNotAdvanceClientKeyWhenPacketValidationFails()
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var corrupt = new ClientPayloadEncryptor(0x01020304).Encrypt(CreateClientPayload(37, 5001, 0));
		corrupt[2] ^= 0x7f;
		var nextValid = new ClientPayloadEncryptor(0x01020304).Encrypt(CreateClientPayload(236, 5001, 0, 1));

		Assert.False(crypt.DecryptClientPayload(corrupt));
		Assert.True(crypt.DecryptClientPayload(nextValid));
		Assert.Equal(CreateClientPayload(236, 5001, 0, 1), nextValid);
	}

	private static byte[] CreateClientPayload(int opcode, params int[] values)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		foreach (var value in values)
		{
			if (value <= byte.MaxValue)
				buffer.WriteC(value);
			else
				buffer.WriteD(value);
		}

		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static byte[] CreateServerFrame(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		buffer.WriteH(0);
		var encodedOpcode = GameCrypt.EncodeServerPacketOpcode(opcode);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x44);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		var frame = buffer.ToArray();
		BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, 2), (ushort)frame.Length);
		return frame;
	}

	private static int GetEncodedKey(int key)
	{
		return unchecked((key ^ unchecked((int)0xCD92E4DF)) + 0x3FF2CCCF);
	}

	private sealed class ClientPayloadEncryptor
	{
		private static readonly byte[] StaticKey = Encoding.ASCII.GetBytes("nKO/WctQ0AVLbpzfBkS6NevDYT8ourG5CRlmdjyJ72aswx4EPq1UgZhFMXH?3iI9");
		private readonly byte[] _clientKey;

		public ClientPayloadEncryptor(int baseKey)
		{
			_clientKey =
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
		}

		public byte[] Encrypt(byte[] payload)
		{
			var data = payload.ToArray();
			if (data.Length == 0)
				return data;

			data[0] ^= _clientKey[0];
			var previous = data[0];

			for (var i = 1; i < data.Length; i++)
			{
				data[i] ^= (byte)(StaticKey[i & 63] ^ _clientKey[i & 7] ^ previous);
				previous = data[i];
			}

			UpdateKey(data.Length);
			return data;
		}

		private void UpdateKey(int packetSize)
		{
			var oldKey = BinaryPrimitives.ReadUInt64LittleEndian(_clientKey);
			oldKey += (uint)packetSize;
			BinaryPrimitives.WriteUInt64LittleEndian(_clientKey, oldKey);
		}
	}

	private sealed class ServerPayloadEncryptor
	{
		private static readonly byte[] StaticKey = Encoding.ASCII.GetBytes("nKO/WctQ0AVLbpzfBkS6NevDYT8ourG5CRlmdjyJ72aswx4EPq1UgZhFMXH?3iI9");
		private readonly byte[] _serverKey;

		public ServerPayloadEncryptor(int baseKey)
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
		}

		public void Encrypt(Span<byte> data)
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

			UpdateKey(data.Length);
		}

		private void UpdateKey(int packetSize)
		{
			var oldKey = BinaryPrimitives.ReadUInt64LittleEndian(_serverKey);
			oldKey += (uint)packetSize;
			BinaryPrimitives.WriteUInt64LittleEndian(_serverKey, oldKey);
		}
	}
}
