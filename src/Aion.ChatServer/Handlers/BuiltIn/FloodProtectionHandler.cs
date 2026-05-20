using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;

namespace Aion.ChatServer.Handlers.BuiltIn;

[ChatHandler("flood_protection")]
public sealed class FloodProtectionHandler : IChatMessageHandler
{
	public int Order => 0;

	public Task HandleAsync(Message message, ChatClient sender, Channel channel, CancellationToken cancellationToken = default)
	{
		var floodProtectionTime = sender.NextMessageTimeSeconds(channel.ChannelType);
		if (floodProtectionTime > 0)
		{
			throw new HandlerVetoException(
				$"You can chat again in this channel in {floodProtectionTime} second{(floodProtectionTime == 1 ? "." : "s.")}");
		}

		sender.UpdateLastMessageTime(channel.ChannelType);
		return Task.CompletedTask;
	}
}
