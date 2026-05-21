using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmMacroDelete : GameClientPacket
{
	public CmMacroDelete(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int MacroPosition { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_MACRO_DELETE.readImpl.
		MacroPosition = buffer.ReadC();
	}
}
