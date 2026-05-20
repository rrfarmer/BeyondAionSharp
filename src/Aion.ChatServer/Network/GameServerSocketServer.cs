using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Aion.ChatServer.Configuration;
using Aion.ChatServer.Network.Handlers;
using Aion.ChatServer.Services;
using Aion.Commons.Network.Server;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Network;

public sealed class GameServerSocketServer : BaseSocketServer
{
	private readonly IGameServerService _gameServerService;
	private readonly IChatService _chatService;
	private readonly ChatServerOptions _options;
	private readonly ConcurrentDictionary<string, GsConnection> _connections = new();
	private long _nextClientId;

	public GameServerSocketServer(
		ILogger<GameServerSocketServer> logger,
		ChatServerOptions options,
		IGameServerService gameServerService,
		IChatService chatService)
		: base(logger, "Aion Chat GameServer Bridge", options.GameServerEndPoint.Address, options.GameServerEndPoint.Port)
	{
		_options = options;
		_gameServerService = gameServerService;
		_chatService = chatService;
	}

	public IPEndPoint? LocalEndPoint => _listener?.LocalEndpoint as IPEndPoint;

	protected override async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
	{
		var clientId = $"chat-gameserver-{Interlocked.Increment(ref _nextClientId)}";
		GsConnection? connection = null;
		try
		{
			connection = new GsConnection(_logger, client, clientId, _gameServerService, _chatService, _options);
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
