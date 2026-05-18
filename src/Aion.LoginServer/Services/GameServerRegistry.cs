using System.Collections.Concurrent;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network.GameServer;

namespace Aion.LoginServer.Services;

public interface IGameServerRegistry
{
	IReadOnlyCollection<GameServerInfo> GetGameServers();

	GameServerInfo? GetGameServer(byte serverId);

	void RegisterKnownServer(GameServerInfo gameServerInfo);

	GsAuthResponse RegisterGameServer(GameServerAuthRequest request, string remoteAddress);
}

public sealed record GameServerAuthRequest(byte ServerId, string Password, byte[] Ip, ushort Port, byte MinAccessLevel, int MaxPlayers);

public sealed class GameServerRegistry : IGameServerRegistry
{
	private readonly ConcurrentDictionary<byte, GameServerInfo> _gameServers = new();

	public IReadOnlyCollection<GameServerInfo> GetGameServers() => _gameServers.Values.OrderBy(server => server.Id).ToArray();

	public GameServerInfo? GetGameServer(byte serverId)
	{
		_gameServers.TryGetValue(serverId, out var server);
		return server;
	}

	public void RegisterKnownServer(GameServerInfo gameServerInfo)
	{
		_gameServers[gameServerInfo.Id] = gameServerInfo;
	}

	public GsAuthResponse RegisterGameServer(GameServerAuthRequest request, string remoteAddress)
	{
		if (!_gameServers.TryGetValue(request.ServerId, out var server))
			return GsAuthResponse.NOT_AUTHED;

		if (server.IsOnline)
			return GsAuthResponse.ALREADY_REGISTERED;

		if (!string.Equals(server.Password, request.Password, StringComparison.Ordinal))
			return GsAuthResponse.NOT_AUTHED;

		// Full Java parity also checks ipMask against remoteAddress; this stays explicit until DB-loaded masks exist.
		server.MarkOnline(request.Ip, request.Port, request.MinAccessLevel, request.MaxPlayers);
		return GsAuthResponse.AUTHED;
	}
}
