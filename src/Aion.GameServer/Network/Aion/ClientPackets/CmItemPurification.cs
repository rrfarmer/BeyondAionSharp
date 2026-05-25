using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmItemPurification : GameClientPacket
{
	public CmItemPurification(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int PlayerObjectId { get; private set; }

	public int BaseItemObjectId { get; private set; }

	public int ResultItemId { get; private set; }

	public IReadOnlyList<int> RequiredMaterialObjectIds { get; private set; } = Array.Empty<int>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_ITEM_PURIFICATION.readImpl reads five required
		// material object ids but runImpl ignores them and decreases materials by item id.
		PlayerObjectId = buffer.ReadD();
		BaseItemObjectId = buffer.ReadD();
		ResultItemId = buffer.ReadD();
		RequiredMaterialObjectIds =
		[
			buffer.ReadD(),
			buffer.ReadD(),
			buffer.ReadD(),
			buffer.ReadD(),
			buffer.ReadD(),
		];
	}
}
