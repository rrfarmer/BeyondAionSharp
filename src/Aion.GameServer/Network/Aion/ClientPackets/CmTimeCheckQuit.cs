namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmTimeCheckQuit : CmTimeCheck
{
	public CmTimeCheckQuit(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
		// Java parity: network/aion/clientpackets/CM_TIME_CHECK_QUIT extends CM_TIME_CHECK with identical behavior.
	}
}
