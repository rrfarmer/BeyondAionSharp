using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCheckPak : GameClientPacket
{
	public CmCheckPak(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte Unknown { get; private set; }

	public string PakStatus { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHECK_PAK.readImpl.
		Unknown = buffer.ReadC();
		PakStatus = buffer.ReadS();
	}
}
