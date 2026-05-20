using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network.Handlers;

namespace Aion.ChatServer.Services;

public interface IChatService
{
	ChatClient RegisterPlayer(int playerId, string accountName, string nick, Race race, byte accessLevel);

	ChatClient? GetPlayer(int playerId);

	bool RegisterPlayerConnection(int playerId, byte[] token, byte[] identifier, string name, string accountName, IChatClientConnection connection);

	Channel? RegisterPlayerWithChannel(ChatClient client, int channelRequestId, string identifier);

	ChatClient? PlayerLogout(int playerId);

	void GagPlayer(int playerId, long gagTimeMillis);
}
