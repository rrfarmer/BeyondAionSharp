using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Aion.ChatServer.Configuration;
using Aion.ChatServer.Handlers;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network.Handlers;
using Aion.ChatServer.Services;
using Aion.Commons.Network.Server;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Network;

public sealed class ClientSocketServer : BaseSocketServer
{
	private readonly IChatService _chatService;
	private readonly ChatChannels _channels;
	private readonly IBroadcastService _broadcastService;
	private readonly ChatHandlerRegistry _handlerRegistry;
	private readonly ChatServerOptions _options;
	private readonly ConcurrentDictionary<string, ClientChannelHandler> _connections = new();
	private long _nextClientId;

	public ClientSocketServer(
		ILogger<ClientSocketServer> logger,
		ChatServerOptions options,
		IChatService chatService,
		ChatChannels channels,
		IBroadcastService broadcastService,
		ChatHandlerRegistry handlerRegistry)
		: base(logger, "Aion Chat Client Server", options.ClientEndPoint.Address, options.ClientEndPoint.Port)
	{
		_options = options;
		_chatService = chatService;
		_channels = channels;
		_broadcastService = broadcastService;
		_handlerRegistry = handlerRegistry;
	}

	public IPEndPoint? LocalEndPoint => _listener?.LocalEndpoint as IPEndPoint;

	protected override async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
	{
		var clientId = $"chat-client-{Interlocked.Increment(ref _nextClientId)}";
		ClientChannelHandler? connection = null;
		try
		{
			connection = new ClientChannelHandler(_logger, client, clientId, _chatService, _channels, _broadcastService, _handlerRegistry, _options);
			_connections[clientId] = connection;
			await connection.RunAsync();
		}
		finally
		{
			if (connection != null)
			{
				_connections.TryRemove(clientId, out _);
				await connection.DisposeAsync();
			}

			ConnectionClosed();
		}
	}

	protected override Task CloseActiveConnectionsAsync()
	{
		var closeTasks = _connections.Values.Select(connection => connection.CloseAsync()).ToArray();
		return closeTasks.Length == 0 ? Task.CompletedTask : Task.WhenAll(closeTasks);
	}
}
