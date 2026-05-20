using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmSendMail : GameClientPacket
{
	public CmSendMail(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public string RecipientName { get; private set; } = string.Empty;

	public string Title { get; private set; } = string.Empty;

	public string Message { get; private set; } = string.Empty;

	public int ItemObjectId { get; private set; }

	public long ItemCount { get; private set; }

	public long KinahCount { get; private set; }

	public int LetterTypeId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_SEND_MAIL.readImpl.
		RecipientName = buffer.ReadS();
		Title = buffer.ReadS();
		Message = buffer.ReadS();
		ItemObjectId = buffer.ReadD();
		ItemCount = buffer.ReadQ();
		KinahCount = buffer.ReadQ();
		LetterTypeId = buffer.ReadC();
	}
}
