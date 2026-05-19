using System.Security.Cryptography;

namespace Aion.LoginServer.Network.Crypto;

public interface ILoginKeyGenerator
{
	LoginRsaKeyPair GetEncryptedRsaKeyPair();

	byte[] GenerateBlowfishKey();
}

public sealed class LoginKeyGenerator : ILoginKeyGenerator, IDisposable
{
	private const int RsaKeyPairCount = 10;
	private readonly LoginRsaKeyPair[] _rsaKeyPairs;

	public LoginKeyGenerator()
	{
		_rsaKeyPairs = Enumerable.Range(0, RsaKeyPairCount).Select(_ => LoginRsaKeyPair.Generate()).ToArray();
	}

	public LoginRsaKeyPair GetEncryptedRsaKeyPair()
	{
		return _rsaKeyPairs[RandomNumberGenerator.GetInt32(_rsaKeyPairs.Length)];
	}

	public byte[] GenerateBlowfishKey()
	{
		return RandomNumberGenerator.GetBytes(16);
	}

	public void Dispose()
	{
		foreach (var keyPair in _rsaKeyPairs)
			keyPair.Dispose();
	}
}
