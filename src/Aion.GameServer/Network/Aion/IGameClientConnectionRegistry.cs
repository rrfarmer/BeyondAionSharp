using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion;

public interface IGameClientConnectionRegistry
{
	void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection);

	void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection);

	Task<int> BroadcastToVisiblePlayersAsync(WorldPosition sourcePosition, int sourceObjectId, GameServerPacket packet, bool includeSourcePlayer = false);

	Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail);

	Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah);
}
