using System.Collections.Concurrent;
using Aion.ChatServer.Models;
using Aion.ChatServer.Network.Packets.Server;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Services;

public sealed class BroadcastService : IBroadcastService
{
	private readonly ConcurrentDictionary<int, ChatClient> _clients = new();
	private readonly ILogger<BroadcastService> _logger;

	public BroadcastService(ILogger<BroadcastService> logger)
	{
		_logger = logger;
	}

	public void AddClient(ChatClient client)
	{
		_clients[client.ClientId] = client;
	}

	public void RemoveClient(ChatClient client)
	{
		_clients.TryRemove(client.ClientId, out _);
	}

	public IReadOnlyCollection<ChatClient> GetRecipients(Message message)
	{
		return _clients.Values.Where(client => client.IsInChannel(message.Channel)).ToArray();
	}

	public async Task BroadcastMessageAsync(Message message, CancellationToken cancellationToken = default)
	{
		var recipients = GetRecipients(message);
		_logger.LogDebug("Broadcasting chat message in channel {ChannelId} to {Count} recipients", message.Channel.ChannelId, recipients.Count);
		foreach (var recipient in recipients)
		{
			if (recipient.Connection != null)
				await recipient.Connection.SendPacketAsync(new SmChannelMessage(message), cancellationToken);
		}
	}
}
