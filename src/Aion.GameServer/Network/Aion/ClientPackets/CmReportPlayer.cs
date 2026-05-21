using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmReportPlayer : GameClientPacket
{
	public CmReportPlayer(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ReportType { get; private set; }

	public string PlayerName { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_REPORT_PLAYER.readImpl.
		ReportType = buffer.ReadC();
		PlayerName = buffer.ReadS();
	}
}
