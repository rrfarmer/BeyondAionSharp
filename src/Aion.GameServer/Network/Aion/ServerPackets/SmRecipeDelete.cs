using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmRecipeDelete : GameServerPacket
{
	public const int PacketOpCode = 242;
	private readonly int _recipeId;

	public SmRecipeDelete(int recipeId)
		: base(PacketOpCode)
	{
		_recipeId = recipeId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_RECIPE_DELETE.writeImpl.
		buffer.WriteD(_recipeId);
	}
}
