using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmSummonEmotion : GameClientPacket
{
	public CmSummonEmotion(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int SummonObjectId { get; private set; }

	public int EmotionTypeId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_SUMMON_EMOTION.readImpl.
		SummonObjectId = buffer.ReadD();
		EmotionTypeId = buffer.ReadC();
	}
}
