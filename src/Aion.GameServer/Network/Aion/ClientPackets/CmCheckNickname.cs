using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCheckNickname : GameClientPacket
{
	public CmCheckNickname(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public string Nickname { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHECK_NICKNAME.readImpl.
		Nickname = buffer.ReadS();
	}
}
