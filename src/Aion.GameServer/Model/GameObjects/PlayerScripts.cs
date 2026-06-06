using System.IO.Compression;
using System.Text;

namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerScript(int Id, byte[] CompressedBytes, int UncompressedSize)
{
	public bool HasData => CompressedBytes.Length > 0;
}

public sealed class PlayerScripts
{
	public const byte ScriptLimit = 8;

	private readonly int _houseObjectId;
	private readonly PlayerScript[] _scripts = new PlayerScript[ScriptLimit];

	public PlayerScripts(int houseObjectId)
	{
		_houseObjectId = houseObjectId;
		FillEmptyScripts();
	}

	public PlayerScript? Get(int scriptId)
	{
		return IsInvalidScriptId(scriptId) ? null : _scripts[scriptId];
	}

	public bool Set(int scriptId, byte[] compressedXml, int uncompressedSize)
	{
		if (IsInvalidScriptId(scriptId))
			return false;
		if (!TryDecompressAndValidate(compressedXml, uncompressedSize, out _))
			return false;

		_scripts[scriptId] = new PlayerScript(scriptId, compressedXml.ToArray(), uncompressedSize);
		return true;
	}

	public bool Remove(int scriptId)
	{
		if (IsInvalidScriptId(scriptId))
			return false;

		_scripts[scriptId] = new PlayerScript(scriptId, Array.Empty<byte>(), 0);
		return true;
	}

	public static bool TryDecodeXml(byte[] compressedXml, int uncompressedSize, out string scriptXml)
	{
		return TryDecompressAndValidate(compressedXml, uncompressedSize, out scriptXml);
	}

	private void FillEmptyScripts()
	{
		for (var i = 0; i < _scripts.Length; i++)
			_scripts[i] = new PlayerScript(i, Array.Empty<byte>(), 0);
	}

	private bool IsInvalidScriptId(int scriptId)
	{
		return scriptId < 0 || scriptId >= _scripts.Length;
	}

	private static bool TryDecompressAndValidate(byte[] compressedXml, int uncompressedSize, out string scriptXml)
	{
		scriptXml = string.Empty;
		if (compressedXml.Length == 0)
			return true;

		byte[] bytes;
		try
		{
			bytes = Decompress(compressedXml);
		}
		catch
		{
			return false;
		}

		scriptXml = Encoding.Unicode.GetString(bytes);
		return bytes.Length == uncompressedSize;
	}

	private static byte[] Decompress(byte[] bytes)
	{
		using var source = new MemoryStream(bytes);
		using var inflater = new ZLibStream(source, CompressionMode.Decompress);
		using var target = new MemoryStream();
		inflater.CopyTo(target);
		return target.ToArray();
	}
}
