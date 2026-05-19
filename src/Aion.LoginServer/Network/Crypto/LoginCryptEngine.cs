using System.Security.Cryptography;

namespace Aion.LoginServer.Network.Crypto;

/// <summary>
/// Direct port of Java login-server network/ncrypt/CryptEngine.
/// This class keeps Java's packet padding, checksum, first-packet XOR pass,
/// and delayed Blowfish key update behavior for login-client compatibility.
/// </summary>
public sealed class LoginCryptEngine
{
	private static readonly byte[] InitialKey =
	{
		0x6B, 0x60, 0xCB, 0x5B,
		0x82, 0xCE, 0x90, 0xB1,
		0xCC, 0x2B, 0x6C, 0x55,
		0x6C, 0x6C, 0x6C, 0x6C
	};

	private readonly Func<int> _nextInt;
	private byte[] _key = (byte[])InitialKey.Clone();
	private bool _updatedKey;
	private readonly BlowfishCipher _cipher;

	public LoginCryptEngine()
		: this(NextRandomInt)
	{
	}

	public LoginCryptEngine(Func<int> nextInt)
	{
		_nextInt = nextInt;
		_cipher = new BlowfishCipher(_key);
	}

	public void UpdateKey(byte[] newKey)
	{
		if (newKey.Length == 0)
			throw new ArgumentException("Blowfish key cannot be empty.", nameof(newKey));
		_key = (byte[])newKey.Clone();
	}

	public bool Decrypt(byte[] data, int offset, int length)
	{
		_cipher.Decipher(data, offset, length);
		return VerifyChecksum(data, offset, length);
	}

	public int Encrypt(byte[] data, int offset, int length)
	{
		length += 4;

		if (!_updatedKey)
		{
			length += 4;
			length += 8 - length % 8;
			EnsureCapacity(data, offset, length);
			EncXorPass(data, offset, length, _nextInt());
			_cipher.Cipher(data, offset, length);
			_cipher.UpdateKey(_key);
			_updatedKey = true;
		}
		else
		{
			length += 8 - length % 8;
			EnsureCapacity(data, offset, length);
			AppendChecksum(data, offset, length);
			_cipher.Cipher(data, offset, length);
		}

		return length;
	}

	private static bool VerifyChecksum(byte[] data, int offset, int length)
	{
		unchecked
		{
			if ((length & 3) != 0 || length <= 4)
				return false;

			long chksum = 0;
			var count = offset + length - 4;
			long check;
			int i;

			for (i = offset; i < count; i += 4)
			{
				check = data[i] & 0xFFL;
				check |= data[i + 1] << 8 & 0xFF00L;
				check |= data[i + 2] << 0x10 & 0xFF0000L;
				check |= data[i + 3] << 0x18 & 0xFF000000L;
				chksum ^= check;
			}

			check = data[i] & 0xFFL;
			check |= data[i + 1] << 8 & 0xFF00L;
			check |= data[i + 2] << 0x10 & 0xFF0000L;
			check |= data[i + 3] << 0x18 & 0xFF000000L;

			return chksum == 0;
		}
	}

	private static void AppendChecksum(byte[] raw, int offset, int length)
	{
		unchecked
		{
			long chksum = 0;
			var count = length - 4;
			long ecx;
			int i;

			for (i = offset; i < count; i += 4)
			{
				ecx = raw[i] & 0xFFL;
				ecx |= raw[i + 1] << 8 & 0xFF00L;
				ecx |= raw[i + 2] << 0x10 & 0xFF0000L;
				ecx |= raw[i + 3] << 0x18 & 0xFF000000L;
				chksum ^= ecx;
			}

			ecx = raw[i] & 0xFFL;
			ecx |= raw[i + 1] << 8 & 0xFF00L;
			ecx |= raw[i + 2] << 0x10 & 0xFF0000L;
			ecx |= raw[i + 3] << 0x18 & 0xFF000000L;
			raw[i] = (byte)(chksum & 0xFF);
			raw[i + 1] = (byte)(chksum >> 0x08 & 0xFF);
			raw[i + 2] = (byte)(chksum >> 0x10 & 0xFF);
			raw[i + 3] = (byte)(chksum >> 0x18 & 0xFF);
		}
	}

	private static void EncXorPass(byte[] data, int offset, int length, int key)
	{
		unchecked
		{
			var stop = length - 8;
			var pos = 4 + offset;
			var ecx = key;

			while (pos < stop)
			{
				var edx = data[pos] & 0xFF;
				edx |= (data[pos + 1] & 0xFF) << 8;
				edx |= (data[pos + 2] & 0xFF) << 16;
				edx |= (data[pos + 3] & 0xFF) << 24;
				ecx += edx;
				edx ^= ecx;
				data[pos++] = (byte)(edx & 0xFF);
				data[pos++] = (byte)(edx >> 8 & 0xFF);
				data[pos++] = (byte)(edx >> 16 & 0xFF);
				data[pos++] = (byte)(edx >> 24 & 0xFF);
			}

			data[pos++] = (byte)(ecx & 0xFF);
			data[pos++] = (byte)(ecx >> 8 & 0xFF);
			data[pos++] = (byte)(ecx >> 16 & 0xFF);
			data[pos] = (byte)(ecx >> 24 & 0xFF);
		}
	}

	private static int NextRandomInt()
	{
		Span<byte> bytes = stackalloc byte[4];
		RandomNumberGenerator.Fill(bytes);
		return BitConverter.ToInt32(bytes);
	}

	private static void EnsureCapacity(byte[] data, int offset, int length)
	{
		if (offset < 0 || length < 0 || offset + length > data.Length)
			throw new ArgumentOutOfRangeException(nameof(length), "CryptEngine output does not fit in the provided packet buffer.");
	}
}
