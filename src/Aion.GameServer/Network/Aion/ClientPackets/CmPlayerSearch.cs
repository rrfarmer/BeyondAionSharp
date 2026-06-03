using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmPlayerSearch : GameClientPacket
{
	public CmPlayerSearch(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_PLAYER_SEARCH.readImpl fields for player search panel.
	public string Name { get; private set; } = string.Empty;
	public int Region { get; private set; }
	public int ClassMask { get; private set; }
	public byte MinLevel { get; private set; }
	public byte MaxLevel { get; private set; }
	public byte LfgOnly { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_PLAYER_SEARCH.readImpl.
		Name = buffer.ReadS(); // Java: readS(25) — max 25 chars, null-terminated
		Region = buffer.ReadD();
		ClassMask = buffer.ReadD();
		MinLevel = buffer.ReadC();
		MaxLevel = buffer.ReadC();
		LfgOnly = buffer.ReadC();
		buffer.ReadC(); // unk: 0x00 in search, 0x30 in /who
	}
}
