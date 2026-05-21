using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmMacroCreate : GameClientPacket
{
	public CmMacroCreate(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int MacroPosition { get; private set; }

	public string MacroXml { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_MACRO_CREATE.readImpl.
		MacroPosition = buffer.ReadC();
		MacroXml = buffer.ReadS();
	}
}
