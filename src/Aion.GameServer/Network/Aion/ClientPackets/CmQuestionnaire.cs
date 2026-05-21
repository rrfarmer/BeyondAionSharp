using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmQuestionnaire : GameClientPacket
{
	public CmQuestionnaire(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int ObjectId { get; private set; }

	public IReadOnlyList<int> ItemIds { get; private set; } = Array.Empty<int>();

	public string StringItemsId { get; private set; } = string.Empty;

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_QUESTIONNAIRE.readImpl.
		ObjectId = buffer.ReadD();
		var itemSize = buffer.ReadH();
		var items = new int[itemSize];
		for (var i = 0; i < items.Length; i++)
			items[i] = buffer.ReadD();
		ItemIds = items;
		StringItemsId = buffer.ReadS();
	}
}
