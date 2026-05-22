using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLearnRecipe : GameServerPacket
{
	public const int PacketOpCode = 241;
	private readonly int _recipeId;

	public SmLearnRecipe(int recipeId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_LEARN_RECIPE.
		_recipeId = recipeId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_LEARN_RECIPE.writeImpl.
		buffer.WriteD(_recipeId);
		buffer.WriteC(0);
	}
}
