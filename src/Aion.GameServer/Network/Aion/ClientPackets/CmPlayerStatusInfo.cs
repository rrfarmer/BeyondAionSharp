using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmPlayerStatusInfo : GameClientPacket
{
	public CmPlayerStatusInfo(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int CommandCode { get; private set; }

	public int SelectedObjectId { get; private set; }

	public int AllianceGroupId { get; private set; }

	public int SecondObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_PLAYER_STATUS_INFO.readImpl.
		CommandCode = buffer.ReadC();
		SelectedObjectId = buffer.ReadD();
		AllianceGroupId = buffer.ReadD();
		SecondObjectId = buffer.ReadD();
	}
}
