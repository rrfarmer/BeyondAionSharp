using System;

namespace Aion.Commons.Crypto
{
	/// <summary>
	/// Aion XOR-based packet cipher.
	/// Must replicate Java's Crypt behavior exactly for client compatibility.
	/// Uses little-endian byte ordering and per-packet key rotation.
	/// </summary>
	public class AionXorCipher
	{
		// Static XOR key - embedded in both Java and C# clients
		// This is the 64-character hex string from game-server/network/Crypt.java
		private static readonly byte[] STATIC_KEY = System.Text.Encoding.ASCII.GetBytes(
			"70D6D6D670D6D6D670D6D6D670D6D6D6" + "70D6D6D670D6D6D670D6D6D670D6D6D6"
		);

		private readonly byte[] _key;
		private int _keyIndex;

		/// <summary>
		/// Create a new cipher instance with fresh key.
		/// </summary>
		public AionXorCipher()
		{
			_key = new byte[STATIC_KEY.Length];
			Array.Copy(STATIC_KEY, _key, STATIC_KEY.Length);
			_keyIndex = 0;
		}

		/// <summary>
		/// XOR-encrypt data in-place. Key index advances with each byte.
		/// </summary>
		public void Encrypt(byte[] data, int offset, int length)
		{
			for (int i = offset; i < offset + length; i++)
			{
				data[i] ^= _key[_keyIndex % _key.Length];
				_keyIndex = (_keyIndex + 1) % _key.Length;
			}
		}

		/// <summary>
		/// XOR-decrypt data in-place. XOR is symmetric, so this is identical to Encrypt.
		/// </summary>
		public void Decrypt(byte[] data, int offset, int length)
		{
			Encrypt(data, offset, length);
		}

		/// <summary>
		/// Rotate the key state to match game protocol version handshake.
		/// </summary>
		public void RotateKey(int version)
		{
			// Advance key index by version bytes to introduce version-dependent state
			_keyIndex = (_keyIndex + (version & 0xFF)) % _key.Length;
		}

		/// <summary>
		/// Reset cipher to initial state.
		/// </summary>
		public void Reset()
		{
			Array.Copy(STATIC_KEY, _key, STATIC_KEY.Length);
			_keyIndex = 0;
		}

		/// <summary>
		/// Get current key index position (for debugging/testing).
		/// </summary>
		public int GetKeyIndex() => _keyIndex;
	}

	/// <summary>
	/// Opcode obfuscation/deobfuscation.
	/// Server and client opcodes are XOR-masked with version for security.
	/// </summary>
	public static class OpcodeObfuscator
	{
		/// <summary>
		/// Obfuscate an opcode using the server version.
		/// </summary>
		public static int ObfuscateOpcode(int opcode, int serverVersion)
		{
			return opcode ^ (serverVersion & 0xFFFF);
		}

		/// <summary>
		/// Deobfuscate an opcode using the server version.
		/// </summary>
		public static int DeobfuscateOpcode(int obfuscatedOpcode, int serverVersion)
		{
			return obfuscatedOpcode ^ (serverVersion & 0xFFFF);
		}
	}

	/// <summary>
	/// Encryption key pair for storing server/client keys and rotation state.
	/// </summary>
	public class EncryptionKeyPair
	{
		/// <summary>
		/// Server-to-client cipher.
		/// </summary>
		public AionXorCipher ServerCipher { get; }

		/// <summary>
		/// Client-to-server cipher (separate state).
		/// </summary>
		public AionXorCipher ClientCipher { get; }

		public EncryptionKeyPair()
		{
			ServerCipher = new AionXorCipher();
			ClientCipher = new AionXorCipher();
		}

		/// <summary>
		/// Initialize key pair with protocol version.
		/// </summary>
		public void Initialize(int serverVersion)
		{
			ServerCipher.RotateKey(serverVersion);
			ClientCipher.RotateKey(serverVersion);
		}

		/// <summary>
		/// Reset both ciphers to initial state.
		/// </summary>
		public void Reset()
		{
			ServerCipher.Reset();
			ClientCipher.Reset();
		}
	}
}
