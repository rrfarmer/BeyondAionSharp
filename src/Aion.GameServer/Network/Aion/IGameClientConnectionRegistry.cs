using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion;

public interface IGameClientConnectionRegistry
{
	bool TryGetOnlinePlayerByName(string playerName, out Player? player);

	void ForEachOnlinePlayer(Action<Player> action);

	Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet);

	Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null);

	Task<int> BroadcastToVisiblePlayersAsync(
		WorldPosition sourcePosition,
		int sourceObjectId,
		GameServerPacket packet,
		bool includeSourcePlayer = false,
		Func<Player, bool>? filter = null);

	Task<int> RefreshHousingVisibilityAsync(
		IReadOnlyList<WorldHouse> houses,
		HousingTemplateTable? housingTemplates,
		int? playerObjectId = null);

	Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null);

	Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates);

	Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail);

	Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah);
}
