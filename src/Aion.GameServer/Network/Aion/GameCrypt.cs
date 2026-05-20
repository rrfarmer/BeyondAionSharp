using System.Security.Cryptography;

namespace Aion.GameServer.Network.Aion;

public sealed class GameCrypt
{
	private const int InternalVersion = 207;
	private readonly Func<int> _keyGenerator;
	private GameEncryptionKeyPair? _packetKey;

	public GameCrypt(Func<int>? keyGenerator = null)
	{
		_keyGenerator = keyGenerator ?? (() => RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue));
	}

	public bool IsEnabled { get; private set; }

	public int EnableKey()
	{
		// Java parity: network/aion/GameCrypt.generateKey used by SM_KEY.
		if (_packetKey != null)
			throw new InvalidOperationException("Key is already initialized");

		var key = _keyGenerator();
		_packetKey = new GameEncryptionKeyPair(key);
		return unchecked((key ^ unchecked((int)0xCD92E4DF)) + 0x3FF2CCCF);
	}

	public void EncryptServerPayload(Span<byte> payload)
	{
		// Java parity: network/aion/GameCrypt.encrypt with first packet enabling the key.
		if (!IsEnabled)
		{
			if (_packetKey == null)
				throw new InvalidOperationException("The first game server packet must initialize the crypt key.");

			IsEnabled = true;
			return;
		}

		if (_packetKey == null)
			throw new InvalidOperationException("Crypt key is not initialized.");

		_packetKey.EncryptServer(payload);
	}

	public bool DecryptClientPayload(Span<byte> payload)
	{
		// Java parity: network/aion/GameCrypt.decrypt.
		if (_packetKey == null)
			throw new InvalidOperationException("Crypt key is not initialized.");

		return _packetKey.DecryptClient(payload);
	}

	public static int EncodeServerPacketOpcode(int opcode)
	{
		// Java parity: network/aion/ServerPacketsOpcodes internal version opcode transform.
		return (opcode + InternalVersion) ^ 0xDF;
	}

	public static int DecodeClientPacketOpcode(int opcode)
	{
		// Java parity: network/aion/AionClientPacketFactory opcode transform.
		return (((opcode ^ 0xEF) - 0x0C) ^ 0xEF) - InternalVersion;
	}
}
