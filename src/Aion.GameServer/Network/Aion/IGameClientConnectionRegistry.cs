using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion;

public interface IGameClientConnectionRegistry
{
	void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection);

	void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection);

	Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail);

	Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah);
}
