using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmRecipeList : GameServerPacket
{
	public const int PacketOpCode = 207;

	private readonly IReadOnlyList<int> _recipeIds;

	public SmRecipeList(IReadOnlyList<int> recipeIds)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_RECIPE_LIST(Set<Integer> recipeIds).
		_recipeIds = recipeIds;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_RECIPE_LIST.writeImpl.
		buffer.WriteH(_recipeIds.Count);
		foreach (var recipeId in _recipeIds)
		{
			buffer.WriteD(recipeId);
			buffer.WriteC(0);
		}
	}
}
