namespace Aion.LoginServer.Network.Aion;

public sealed record LoginCredentials(string Username, string Password, int OneTimePassword)
{
	public static LoginCredentials FromDecryptedBlocks(byte[] decrypted, bool isLoginEx)
	{
		var contentStartOffset = isLoginEx ? 78 : 94;
		var usernameByteLength = isLoginEx ? 64 : 14;
		var passwordByteLength = isLoginEx ? 32 : 16;
		var compacted = (byte[])decrypted.Clone();

		for (var offset = decrypted.Length - 128; offset >= 0; offset -= 128)
		{
			var source = offset + contentStartOffset;
			var length = compacted.Length - source;
			if (length > 0)
				Array.Copy(compacted, source, compacted, offset, length);
		}

		var username = ReadNullTerminatedCp1252(compacted, 0, usernameByteLength);
		var password = ReadNullTerminatedCp1252(compacted, usernameByteLength, passwordByteLength);
		var otp = BitConverter.ToInt32(compacted, usernameByteLength + passwordByteLength);
		return new LoginCredentials(username, password, otp);
	}

	private static string ReadNullTerminatedCp1252(byte[] bytes, int offset, int maxLength)
	{
		var length = 0;
		while (length < maxLength && offset + length < bytes.Length && bytes[offset + length] != 0)
			length++;

		var chars = new char[length];
		for (var i = 0; i < length; i++)
			chars[i] = DecodeCp1252(bytes[offset + i]);
		return new string(chars);
	}

	private static char DecodeCp1252(byte value)
	{
		if (value < 0x80 || value >= 0xA0)
			return (char)value;

		return value switch
		{
			0x80 => '\u20AC',
			0x82 => '\u201A',
			0x83 => '\u0192',
			0x84 => '\u201E',
			0x85 => '\u2026',
			0x86 => '\u2020',
			0x87 => '\u2021',
			0x88 => '\u02C6',
			0x89 => '\u2030',
			0x8A => '\u0160',
			0x8B => '\u2039',
			0x8C => '\u0152',
			0x8E => '\u017D',
			0x91 => '\u2018',
			0x92 => '\u2019',
			0x93 => '\u201C',
			0x94 => '\u201D',
			0x95 => '\u2022',
			0x96 => '\u2013',
			0x97 => '\u2014',
			0x98 => '\u02DC',
			0x99 => '\u2122',
			0x9A => '\u0161',
			0x9B => '\u203A',
			0x9C => '\u0153',
			0x9E => '\u017E',
			0x9F => '\u0178',
			_ => '\uFFFD'
		};
	}
}
