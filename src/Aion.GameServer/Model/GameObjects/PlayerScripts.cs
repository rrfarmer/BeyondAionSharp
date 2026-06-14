using System.IO.Compression;
using System.Text;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/gameobjects/player/PlayerScripts (@author Rolandas, Neon, Sykra). Uses the faithful
// model/house/PlayerScript record (the previously-duplicated GameObjects.PlayerScript record was removed to
// unify on Model.House.PlayerScript, which SM_HOUSE_SCRIPTS consumes). DB persistence stays DAO-owned in this port.
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

	// Java parity: PlayerScripts.sendToPlayer(Player, int) — split the script array into packet-body-sized parts
	// and send one SM_HOUSE_SCRIPTS per part.
	public void SendToPlayer(Player player, int houseAddress)
	{
		if (player == null)
			return;
		SplitList<PlayerScript> scriptSplitList = new DynamicServerPacketBodySplitList<PlayerScript>(
			new List<PlayerScript>(_scripts), false, SM_HOUSE_SCRIPTS.STATIC_BODY_SIZE,
			SM_HOUSE_SCRIPTS.DYNAMIC_BODY_PART_SIZE_CALCULATOR);
		foreach (var part in scriptSplitList)
			PacketSendUtility.SendPacket(player, new SM_HOUSE_SCRIPTS(houseAddress, part));
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
