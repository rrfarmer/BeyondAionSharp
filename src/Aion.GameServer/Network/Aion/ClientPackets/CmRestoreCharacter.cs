using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmRestoreCharacter : GameClientPacket
{
	public CmRestoreCharacter(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int PlayOk2 { get; private set; }

	public int CharacterObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_RESTORE_CHARACTER.readImpl.
		PlayOk2 = buffer.ReadD();
		CharacterObjectId = buffer.ReadD();
	}
}
