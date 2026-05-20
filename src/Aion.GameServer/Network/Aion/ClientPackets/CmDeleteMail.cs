using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmDeleteMail : GameClientPacket
{
	public CmDeleteMail(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public IReadOnlyList<int> MailObjectIds { get; private set; } = Array.Empty<int>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_DELETE_MAIL.readImpl.
		var count = buffer.ReadH();
		var mailObjectIds = new List<int>(count);
		for (var i = 0; i < count; i++)
		{
			mailObjectIds.Add(buffer.ReadD());
			buffer.ReadC();
		}

		MailObjectIds = mailObjectIds;
	}
}
