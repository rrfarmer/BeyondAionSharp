using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;

namespace Aion.ChatServer.Handlers;

public interface IChatMessageHandler
{
	int Order { get; }

	Task HandleAsync(Message message, ChatClient sender, Channel channel, CancellationToken cancellationToken = default);
}
