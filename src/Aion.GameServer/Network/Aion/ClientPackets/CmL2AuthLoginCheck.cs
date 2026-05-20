using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmL2AuthLoginCheck : GameClientPacket
{
	public CmL2AuthLoginCheck(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int PlayOk2 { get; private set; }

	public int PlayOk1 { get; private set; }

	public int AccountId { get; private set; }

	public int LoginOk { get; private set; }

	public int Unknown1 { get; private set; }

	public int Unknown2 { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_L2AUTH_LOGIN_CHECK.readImpl.
		PlayOk2 = buffer.ReadD();
		PlayOk1 = buffer.ReadD();
		AccountId = buffer.ReadD();
		LoginOk = buffer.ReadD();
		Unknown1 = buffer.ReadD();
		Unknown2 = buffer.ReadD();
	}
}
