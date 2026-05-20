using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmMacroList : GameServerPacket
{
	public const int PacketOpCode = 231;
	private const int StaticBodySize = 7;

	private readonly bool _clearList;
	private readonly IReadOnlyList<PlayerMacro> _macros;
	private readonly int _playerObjectId;

	private SmMacroList(int playerObjectId, IReadOnlyList<PlayerMacro> macros, bool clearList)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_MACRO_LIST(int, List<Macro>, boolean).
		_playerObjectId = playerObjectId;
		_macros = macros;
		_clearList = clearList;
	}

	public static IReadOnlyList<SmMacroList> CreateLoginPackets(int playerObjectId, IReadOnlyList<PlayerMacro> macros)
	{
		// Java parity: services/player/PlayerEnterWorldService.sendMacroList.
		if (macros.Count == 0)
			return [new SmMacroList(playerObjectId, Array.Empty<PlayerMacro>(), clearList: true)];

		var packets = new List<SmMacroList>();
		var part = new List<PlayerMacro>();
		var partSize = 0;
		var maxDynamicSize = MaxUsablePacketBodySize - StaticBodySize;
		foreach (var macro in macros)
		{
			var macroSize = GetMacroSize(macro);
			if (macroSize > maxDynamicSize)
				throw new InvalidOperationException($"Macro {macro.Id} exceeds maximum SM_MACRO_LIST body size.");

			if (part.Count > 0 && partSize + macroSize > maxDynamicSize)
			{
				packets.Add(new SmMacroList(playerObjectId, part.ToArray(), clearList: packets.Count == 0));
				part.Clear();
				partSize = 0;
			}

			part.Add(macro);
			partSize += macroSize;
		}

		packets.Add(new SmMacroList(playerObjectId, part.ToArray(), clearList: packets.Count == 0));
		return packets;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_MACRO_LIST.writeImpl.
		buffer.WriteD(_playerObjectId);
		buffer.WriteC(_clearList ? 1 : 0);
		buffer.WriteH(-_macros.Count);
		foreach (var macro in _macros)
		{
			buffer.WriteC(macro.Id);
			buffer.WriteS(macro.Xml);
		}
	}

	private static int GetMacroSize(PlayerMacro macro)
	{
		// Java parity: SM_MACRO_LIST.DYNAMIC_BODY_PART_SIZE_CALCULATOR.
		return 1 + (macro.Xml.Length * 2) + 2;
	}
}
