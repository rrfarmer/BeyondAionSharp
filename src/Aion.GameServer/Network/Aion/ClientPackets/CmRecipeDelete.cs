using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmRecipeDelete : GameClientPacket
{
	public CmRecipeDelete(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int RecipeId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_RECIPE_DELETE.readImpl.
		RecipeId = buffer.ReadD();
	}
}
