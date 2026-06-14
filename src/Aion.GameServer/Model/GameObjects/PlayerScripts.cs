using System.IO.Compression;
using System.Text;

namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerScript(int Id, byte[] CompressedBytes, int UncompressedSize)
{
	public static PlayerScript LuaSandboxFix { get; } = CreateLuaSandboxFix();

	public bool HasData => CompressedBytes.Length > 0;

	private static PlayerScript CreateLuaSandboxFix()
	{
		// Java parity: model/house/PlayerScript.LUA_SANDBOX_FIX.
		var script = """
			<?xml version="1.0" encoding="UTF-16" ?>
			<lboxes>
				<lbox>
					<id>101</id>
					<name><![CDATA[Lua Fix]]></name>
					<desc><![CDATA[Secures the Lua environment against malicious actors]]></desc>
					<script><![CDATA[
			_G.debug = nil
			_G.dofile = nil
			_G.io = nil
			_G.load = nil
			_G.loadfile = nil
			_G.loadstring = nil
			_G.package = nil
			_G.require = nil
			]]></script>
					<icon>1</icon>
				</lbox>
			</lboxes>
			""";
		var bytes = Encoding.Unicode.GetBytes(script);
		return new PlayerScript(0, PlayerScripts.Compress(bytes), bytes.Length);
	}
}

public sealed class PlayerScripts
{
	public const byte ScriptLimit = 8;

	private readonly PlayerScript[] _scripts = new PlayerScript[ScriptLimit];

	public PlayerScripts(int houseObjectId)
	{
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

	// Java parity: PlayerScripts.set(int, byte[], int, boolean storeInDb). The storeInDb branch persists via
	// HousingService/HouseScriptsDAO before caching; here the calling DAO owns persistence, so storeInDb only
	// gates that (callers pass false) and the cache update is identical to the 3-arg overload.
	public bool Set(int scriptId, byte[] compressedXml, int uncompressedSize, bool storeInDb)
	{
		_ = storeInDb;
		return Set(scriptId, compressedXml, uncompressedSize);
	}

	public bool Remove(int scriptId)
	{
		if (IsInvalidScriptId(scriptId))
			return false;

		_scripts[scriptId] = new PlayerScript(scriptId, Array.Empty<byte>(), 0);
		return true;
	}

	// Java parity: PlayerScripts.removeAll() — clears every script slot. (DAO persistence is owned by the
	// calling HousingService/HouseScriptsDAO in this port, matching the inverted Remove/Set ownership above.)
	public void RemoveAll()
	{
		FillEmptyScripts();
	}

	public bool RestoreFromXml(int scriptId, string? scriptXml)
	{
		// Java parity: dao/HouseScriptsDAO.addScript(..., storeInDb=false).
		if (string.IsNullOrEmpty(scriptXml))
			return Set(scriptId, Array.Empty<byte>(), 0);

		var bytes = Encoding.Unicode.GetBytes(scriptXml);
		return Set(scriptId, Compress(bytes), bytes.Length);
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

	public static byte[] Compress(byte[] bytes)
	{
		using var target = new MemoryStream();
		using (var deflater = new ZLibStream(target, CompressionLevel.Optimal, leaveOpen: true))
			deflater.Write(bytes);
		return target.ToArray();
	}
}
