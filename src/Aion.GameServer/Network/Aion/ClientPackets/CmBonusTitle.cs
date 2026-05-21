using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmBonusTitle : GameClientPacket
{
	public CmBonusTitle(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int BonusTitleId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BONUS_TITLE.readImpl.
		BonusTitleId = buffer.ReadH();
	}
}
