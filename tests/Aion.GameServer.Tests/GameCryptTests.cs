using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Aion.Commons.Nio;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

/// <summary>
/// Crypt-framing parity for the GS -> Aion server crypt seam. Exercises the FAITHFUL packets
/// SM_KEY / SM_PONG through their real AionServerPacket.Write(con, buffer) -> con.Encrypt path on
/// an uninitialized AionConnection (RuntimeHelpers.GetUninitializedObject; precedent: the Golden*
/// fixture harnesses) wired with only the field Write/Encrypt touch -- a live faithful Crypt.
///
/// Java is the oracle: network/Crypt + network/EncryptionKeyPair drive the framing/encryption.
/// The faithful Crypt seeds its key from Rnd (non-deterministic), so each case captures the actual
/// baseKey the crypt used (Crypt.packetKey.GetBaseKey via reflection) and independently recomputes
/// the expected enciphered key and the expected encrypted frame from that same baseKey using a
/// verbatim copy of EncryptionKeyPair.Encrypt -- never a hard-coded byte blob.
/// </summary>
public sealed class GameCryptTests
{
	[Fact]
	public void Write_LeavesSmKeyUnencryptedThenEncryptsNextServerPacket()
	{
		var con = NewConnectionWithFreshCrypt();

		// SM_KEY: first server packet. enableCryptKey() generates the key; the frame is NOT encrypted.
		var smKeyFrame = WriteFaithful(new SM_KEY(), con);
		var baseKey = GetBaseKey(con);
		var expectedSmKeyFrame = CreateServerFrame(72, writer => writer.PutInt(GetEncodedKey(baseKey)));

		Assert.Equal(expectedSmKeyFrame, smKeyFrame);
		Assert.True(GetCrypt(con).IsEnabled());

		// SM_PONG: second server packet -> encrypted with the now-enabled key.
		var smPongFrame = WriteFaithful(new SM_PONG(), con);
		var expectedSmPongFrame = CreateServerFrame(142, writer =>
		{
			writer.Put((byte)0);
			writer.Put((byte)0);
		});
		var encryptor = new ServerPayloadEncryptor(baseKey);
		encryptor.Encrypt(expectedSmPongFrame.AsSpan(2));

		Assert.Equal(expectedSmPongFrame, smPongFrame);
	}

	[Fact]
	public void Write_AdvancesServerKeyAcrossEncryptedPackets()
	{
		var con = NewConnectionWithFreshCrypt();
		WriteFaithful(new SM_KEY(), con);
		var baseKey = GetBaseKey(con);

		var encryptor = new ServerPayloadEncryptor(baseKey);
		var expectedFirstPong = CreateEncryptedSmPongFrame(encryptor);
		var expectedSecondPong = CreateEncryptedSmPongFrame(encryptor);

		var firstPong = WriteFaithful(new SM_PONG(), con);
		var secondPong = WriteFaithful(new SM_PONG(), con);

		Assert.Equal(expectedFirstPong, firstPong);
		Assert.Equal(expectedSecondPong, secondPong);
		Assert.NotEqual(firstPong, secondPong);
	}

	[Fact]
	public void DecryptClientPayload_AdvancesClientKeyAfterValidPacket()
	{
		var crypt = NewCryptWithBaseKey(0x01020304);
		var encryptor = new ClientPayloadEncryptor(0x01020304);
		var first = encryptor.Encrypt(CreateClientPayload(37, 5001, 0));
		var second = encryptor.Encrypt(CreateClientPayload(236, 5001, 0, 1));

		Assert.True(DecryptClientPayload(crypt, first));
		Assert.True(DecryptClientPayload(crypt, second));
		Assert.Equal(CreateClientPayload(37, 5001, 0), first);
		Assert.Equal(CreateClientPayload(236, 5001, 0, 1), second);
	}

	[Fact]
	public void DecryptClientPayload_DoesNotAdvanceClientKeyWhenPacketValidationFails()
	{
		var crypt = NewCryptWithBaseKey(0x01020304);
		var corrupt = new ClientPayloadEncryptor(0x01020304).Encrypt(CreateClientPayload(37, 5001, 0));
		corrupt[2] ^= 0x7f;
		var nextValid = new ClientPayloadEncryptor(0x01020304).Encrypt(CreateClientPayload(236, 5001, 0, 1));

		Assert.False(DecryptClientPayload(crypt, corrupt));
		Assert.True(DecryptClientPayload(crypt, nextValid));
		Assert.Equal(CreateClientPayload(236, 5001, 0, 1), nextValid);
	}

	// ---- faithful crypt seam (uninitialized AionConnection wired with only a live Crypt) ----

	/// <summary>
	/// Allocate an uninitialized AionConnection (no socket / Dispatcher) and pin its private
	/// readonly <c>crypt</c> field to a fresh faithful Crypt -- the only field Write/Encrypt touch.
	/// </summary>
	private static AionConnection NewConnectionWithFreshCrypt()
	{
		var con = (AionConnection)RuntimeHelpers.GetUninitializedObject(typeof(AionConnection));
		SetPrivateField(con, "crypt", new Crypt());
		return con;
	}

	/// <summary>Serialize a faithful AionServerPacket through its real Write(con, buffer) framing+encrypt path.</summary>
	private static byte[] WriteFaithful(AionServerPacket packet, AionConnection con)
	{
		var buffer = ByteBuffer.Allocate(AionServerPacket.MAX_CLIENT_SUPPORTED_PACKET_SIZE).Order(ByteOrder.LITTLE_ENDIAN);
		packet.Write(con, buffer);
		// After Write, the framed (and possibly encrypted) bytes occupy [0, limit) of the backing buffer.
		var length = buffer.Limit();
		var frame = new byte[length];
		System.Array.Copy(buffer.Array(), buffer.ArrayOffset(), frame, 0, length);
		return frame;
	}

	private static Crypt GetCrypt(AionConnection con) => (Crypt)GetPrivateField(con, "crypt")!;

	private static int GetBaseKey(AionConnection con)
	{
		var packetKey = GetPrivateField(GetCrypt(con), "packetKey");
		Assert.NotNull(packetKey);
		return ((EncryptionKeyPair)packetKey).GetBaseKey();
	}

	private static Crypt NewCryptWithBaseKey(int baseKey)
	{
		var crypt = new Crypt();
		// Mirror Crypt.EnableKey()'s post-state deterministically (Rnd would pick the baseKey otherwise):
		// install the EncryptionKeyPair and flip isEnabled so DecryptClientPayload can run.
		SetPrivateField(crypt, "packetKey", new EncryptionKeyPair(baseKey));
		SetPrivateField(crypt, "isEnabled", true);
		return crypt;
	}

	private static bool DecryptClientPayload(Crypt crypt, byte[] payload)
	{
		var buffer = ByteBuffer.Allocate(payload.Length).Order(ByteOrder.LITTLE_ENDIAN);
		buffer.Put(payload);
		buffer.Flip();
		var ok = crypt.Decrypt(buffer);
		System.Array.Copy(buffer.Array(), buffer.ArrayOffset(), payload, 0, payload.Length);
		return ok;
	}

	// ---- reflection helpers ----

	private static void SetPrivateField(object target, string name, object value)
	{
		var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(target.GetType().FullName, name);
		field.SetValue(target, value);
	}

	private static object? GetPrivateField(object target, string name)
	{
		var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(target.GetType().FullName, name);
		return field.GetValue(target);
	}

	// ---- expected-frame builders (independent recomputation; Java EncryptionKeyPair is the oracle) ----

	private static byte[] CreateClientPayload(int opcode, params int[] values)
	{
		var buffer = ByteBuffer.Allocate(64).Order(ByteOrder.LITTLE_ENDIAN);
		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		buffer.PutShort((short)encodedOpcode);
		buffer.Put((byte)0x65);
		buffer.PutShort((short)~encodedOpcode);
		foreach (var value in values)
		{
			if (value <= byte.MaxValue)
				buffer.Put((byte)value);
			else
				buffer.PutInt(value);
		}

		var length = buffer.Position();
		var data = new byte[length];
		buffer.Flip();
		buffer.Get(data);
		return data;
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static byte[] CreateServerFrame(int opcode, Action<ByteBuffer> writePayload)
	{
		var buffer = ByteBuffer.Allocate(AionServerPacket.MAX_CLIENT_SUPPORTED_PACKET_SIZE).Order(ByteOrder.LITTLE_ENDIAN);
		buffer.PutShort((short)0);
		var encodedOpcode = Crypt.EncodeServerPacketOpcode(opcode);
		buffer.PutShort((short)encodedOpcode);
		buffer.Put(Crypt.staticServerPacketCode);
		buffer.PutShort((short)~encodedOpcode);
		writePayload(buffer);
		var length = buffer.Position();
		var frame = new byte[length];
		buffer.Flip();
		buffer.Get(frame);
		BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, 2), (ushort)frame.Length);
		return frame;
	}

	private static int GetEncodedKey(int key)
	{
		return unchecked((key ^ unchecked((int)0xCD92E4DF)) + 0x3FF2CCCF);
	}

	private static byte[] CreateEncryptedSmPongFrame(ServerPayloadEncryptor encryptor)
	{
		var frame = CreateServerFrame(142, writer =>
		{
			writer.Put((byte)0);
			writer.Put((byte)0);
		});
		encryptor.Encrypt(frame.AsSpan(2));
		return frame;
	}

	// Verbatim mirror of Java network/EncryptionKeyPair client decrypt-side keystream (oracle).
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

	// Verbatim mirror of Java network/EncryptionKeyPair server encrypt-side keystream (oracle).
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
