using Aion.ChatServer.Models;

namespace Aion.ChatServer.Services;

public interface IBroadcastService
{
	void AddClient(ChatClient client);

	void RemoveClient(ChatClient client);

	IReadOnlyCollection<ChatClient> GetRecipients(Message message);

	Task BroadcastMessageAsync(Message message, CancellationToken cancellationToken = default);
}
