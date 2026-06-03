using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCharacterEdit : GameClientPacket
{
	public CmCharacterEdit(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	// Java parity: CM_CHARACTER_EDIT extends AbstractCharacterEditPacket which reads character objectId + appearance data.
	// Parser kept minimal (stub) since plastic surgery / gender-swap handler is deferred.
	public int ObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CHARACTER_EDIT.readImpl -> readD() for objectId, then appearance.
		// Remaining appearance bytes are unread since handler is deferred.
		if (buffer.Remaining >= 4)
			ObjectId = buffer.ReadD();
	}
}
