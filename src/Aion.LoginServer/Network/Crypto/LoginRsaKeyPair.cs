using System.Numerics;
using System.Security.Cryptography;

namespace Aion.LoginServer.Network.Crypto;

public sealed class LoginRsaKeyPair : IDisposable
{
	private readonly RSA _rsa;
	private readonly RSAParameters _parameters;

	private LoginRsaKeyPair(RSA rsa)
	{
		_rsa = rsa;
		_parameters = rsa.ExportParameters(true);
		if (_parameters.Modulus == null || _parameters.Modulus.Length != 128)
			throw new InvalidOperationException("Login RSA keypair must use a 1024-bit modulus.");

		EncryptedModulus = ScrambleModulus(_parameters.Modulus);
	}

	public byte[] EncryptedModulus { get; }

	public RSAParameters PublicParameters => _rsa.ExportParameters(false);

	public static LoginRsaKeyPair Generate()
	{
		var rsa = RSA.Create();
		rsa.KeySize = 1024;
		var exponent = rsa.ExportParameters(false).Exponent;
		if (exponent == null || exponent.Length != 3 || exponent[0] != 0x01 || exponent[1] != 0x00 || exponent[2] != 0x01)
			throw new InvalidOperationException("Login RSA keypair must use public exponent F4.");
		return new LoginRsaKeyPair(rsa);
	}

	public byte[] DecryptNoPadding(ReadOnlySpan<byte> encryptedBlock)
	{
		if (encryptedBlock.Length != 128)
			throw new ArgumentException("Login RSA encrypted block must be 128 bytes.", nameof(encryptedBlock));
		if (_parameters.Modulus == null || _parameters.D == null)
			throw new InvalidOperationException("RSA private parameters are unavailable.");

		var cipher = new BigInteger(encryptedBlock, isUnsigned: true, isBigEndian: true);
		var privateExponent = new BigInteger(_parameters.D, isUnsigned: true, isBigEndian: true);
		var modulus = new BigInteger(_parameters.Modulus, isUnsigned: true, isBigEndian: true);
		var plain = BigInteger.ModPow(cipher, privateExponent, modulus);
		return ToFixedLengthBigEndian(plain, 128);
	}

	public static byte[] RawEncryptForTesting(ReadOnlySpan<byte> plainBlock, RSAParameters publicParameters)
	{
		if (plainBlock.Length != 128)
			throw new ArgumentException("Login RSA plain block must be 128 bytes.", nameof(plainBlock));
		if (publicParameters.Modulus == null || publicParameters.Exponent == null)
			throw new ArgumentException("RSA public parameters are incomplete.", nameof(publicParameters));

		var plain = new BigInteger(plainBlock, isUnsigned: true, isBigEndian: true);
		var exponent = new BigInteger(publicParameters.Exponent, isUnsigned: true, isBigEndian: true);
		var modulus = new BigInteger(publicParameters.Modulus, isUnsigned: true, isBigEndian: true);
		var cipher = BigInteger.ModPow(plain, exponent, modulus);
		return ToFixedLengthBigEndian(cipher, 128);
	}

	public static byte[] ScrambleModulus(ReadOnlySpan<byte> modulus)
	{
		if (modulus.Length == 129 && modulus[0] == 0)
			modulus = modulus[1..];
		if (modulus.Length != 128)
			throw new ArgumentException("Login RSA modulus must be 128 bytes.", nameof(modulus));

		var encryptedModulus = modulus.ToArray();
		for (var i = 0; i < 4; i++)
			(encryptedModulus[i], encryptedModulus[0x4D + i]) = (encryptedModulus[0x4D + i], encryptedModulus[i]);

		for (var i = 0; i < 0x40; i++)
			encryptedModulus[i] = (byte)(encryptedModulus[i] ^ encryptedModulus[0x40 + i]);

		for (var i = 0; i < 4; i++)
			encryptedModulus[0x0D + i] = (byte)(encryptedModulus[0x0D + i] ^ encryptedModulus[0x34 + i]);

		for (var i = 0; i < 0x40; i++)
			encryptedModulus[0x40 + i] = (byte)(encryptedModulus[0x40 + i] ^ encryptedModulus[i]);

		return encryptedModulus;
	}

	public void Dispose()
	{
		_rsa.Dispose();
	}

	private static byte[] ToFixedLengthBigEndian(BigInteger value, int length)
	{
		var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
		if (bytes.Length > length)
			throw new InvalidOperationException("RSA block is larger than the expected login block size.");

		var result = new byte[length];
		bytes.CopyTo(result.AsSpan(length - bytes.Length));
		return result;
	}
}
