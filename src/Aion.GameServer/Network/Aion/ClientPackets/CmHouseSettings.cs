using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmHouseSettings : GameClientPacket
{
	public CmHouseSettings(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte DoorState { get; private set; }

	public bool ShowOwnerName { get; private set; }

	public string SignNotice { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_SETTINGS.readImpl.
		DoorState = buffer.ReadC();
		ShowOwnerName = buffer.ReadC() == 1;
		SignNotice = buffer.ReadS();
	}
}
