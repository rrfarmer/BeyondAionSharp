using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCraft : GameClientPacket
{
	public CmCraft(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int UnknownByte { get; private set; }

	public int TargetTemplateId { get; private set; }

	public int RecipeId { get; private set; }

	public int TargetObjectId { get; private set; }

	public int CraftType { get; private set; }

	public IReadOnlyDictionary<int, long> MaterialsData { get; private set; } = new Dictionary<int, long>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CRAFT.readImpl.
		UnknownByte = buffer.ReadC();
		TargetTemplateId = buffer.ReadD();
		RecipeId = buffer.ReadD();
		TargetObjectId = buffer.ReadD();
		var materialsCount = buffer.ReadH();
		CraftType = buffer.ReadC();

		var materials = new Dictionary<int, long>(materialsCount);
		for (var i = 0; i < materialsCount; i++)
			materials[buffer.ReadD()] = buffer.ReadQ();
		MaterialsData = materials;
	}
}
