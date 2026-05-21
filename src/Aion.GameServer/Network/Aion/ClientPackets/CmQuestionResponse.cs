using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmQuestionResponse : GameClientPacket
{
	public CmQuestionResponse(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int QuestionId { get; private set; }

	public byte Response { get; private set; }

	public int SenderObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_QUESTION_RESPONSE.readImpl.
		QuestionId = buffer.ReadD();
		Response = buffer.ReadC();
		buffer.ReadC();
		buffer.ReadH();
		SenderObjectId = buffer.ReadD();
		buffer.ReadD();
		buffer.ReadH();
	}
}
