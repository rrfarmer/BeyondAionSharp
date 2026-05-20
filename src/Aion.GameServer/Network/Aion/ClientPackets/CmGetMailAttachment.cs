using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmGetMailAttachment : GameClientPacket
{
	public CmGetMailAttachment(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int MailObjectId { get; private set; }

	public byte AttachmentType { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_GET_MAIL_ATTACHMENT.readImpl.
		MailObjectId = buffer.ReadD();
		AttachmentType = buffer.ReadC();
	}
}
