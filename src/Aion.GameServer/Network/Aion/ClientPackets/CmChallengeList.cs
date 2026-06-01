using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmChallengeList : GameClientPacket
{
	public CmChallengeList(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Action { get; private set; }
	public int TaskOwner { get; private set; }
	public int OwnerType { get; private set; }
	public int PlayerId { get; private set; }
	public int DateSince { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHALLENGE_LIST.readImpl.
		Action = buffer.ReadC();
		TaskOwner = buffer.ReadD();
		OwnerType = buffer.ReadC();
		PlayerId = buffer.ReadD();
		DateSince = buffer.ReadD();
	}
}
