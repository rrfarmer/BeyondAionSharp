using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion;

public interface IGameClientConnectionRegistry
{
	void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection);

	void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection);

	bool TryGetOnlinePlayerByName(string playerName, out Player? player);

	Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet);

	Task<int> BroadcastToVisiblePlayersAsync(
		WorldPosition sourcePosition,
		int sourceObjectId,
		GameServerPacket packet,
		bool includeSourcePlayer = false,
		Func<Player, bool>? filter = null);

	Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail);

	Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah);
}
