using System.Collections.Concurrent;
using Aion.LoginServer.Model;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Network.GameServer.ServerPackets;
using Aion.LoginServer.Utils;

namespace Aion.LoginServer.Services;

public interface IGameServerSession
{
	Task SendPacketAsync(GsServerPacket packet);
}

public interface IGameServerRegistry
{
	IReadOnlyCollection<GameServerInfo> GetGameServers();

	GameServerInfo? GetGameServer(byte serverId);

	void RegisterKnownServer(GameServerInfo gameServerInfo);

	GsAuthResponse RegisterGameServer(GameServerAuthRequest request, string remoteAddress, IGameServerSession? session = null);

	void UnregisterGameServer(byte serverId, IGameServerSession session);

	GameServerInfo? FindLoggedInAccountGameServer(int accountId);

	Task<bool> KickAccountFromGameServerAsync(int accountId, bool notifyDoubleLogin);

	IReadOnlyDictionary<byte, int> GetOfflineGameServerCharacterCounts();

	Task RequestOnlineGameServerCharacterCountsAsync(int accountId);
}

public sealed record GameServerAuthRequest(byte ServerId, string Password, byte[] Ip, ushort Port, byte MinAccessLevel, int MaxPlayers);

public sealed class GameServerRegistry : IGameServerRegistry
{
	private readonly ConcurrentDictionary<byte, GameServerInfo> _gameServers = new();
	private readonly ConcurrentDictionary<byte, IGameServerSession> _onlineSessions = new();

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

	public GsAuthResponse RegisterGameServer(GameServerAuthRequest request, string remoteAddress, IGameServerSession? session = null)
	{
		if (!_gameServers.TryGetValue(request.ServerId, out var server))
			return GsAuthResponse.NOT_AUTHED;

		if (server.IsOnline || _onlineSessions.ContainsKey(request.ServerId))
			return GsAuthResponse.ALREADY_REGISTERED;

		if (!string.Equals(server.Password, request.Password, StringComparison.Ordinal))
			return GsAuthResponse.NOT_AUTHED;

		if (!NetworkMask.Matches(server.IpMask, ExtractIp(remoteAddress)))
			return GsAuthResponse.NOT_AUTHED;

		server.MarkOnline(request.Ip, request.Port, request.MinAccessLevel, request.MaxPlayers);
		if (session != null)
			_onlineSessions[request.ServerId] = session;
		return GsAuthResponse.AUTHED;
	}

	public void UnregisterGameServer(byte serverId, IGameServerSession session)
	{
		if (_onlineSessions.TryGetValue(serverId, out var currentSession) && ReferenceEquals(currentSession, session))
			_onlineSessions.TryRemove(serverId, out _);

		if (_gameServers.TryGetValue(serverId, out var server))
			server.MarkOffline();
	}

	public GameServerInfo? FindLoggedInAccountGameServer(int accountId)
	{
		return GetGameServers().FirstOrDefault(server => server.IsAccountOnGameServer(accountId));
	}

	public async Task<bool> KickAccountFromGameServerAsync(int accountId, bool notifyDoubleLogin)
	{
		var gameServer = FindLoggedInAccountGameServer(accountId);
		if (gameServer == null || !_onlineSessions.TryGetValue(gameServer.Id, out var session))
			return false;

		await session.SendPacketAsync(new SmRequestKickAccount(accountId, notifyDoubleLogin));
		return true;
	}

	public IReadOnlyDictionary<byte, int> GetOfflineGameServerCharacterCounts()
	{
		return GetGameServers()
			.Where(server => !server.IsOnline || !_onlineSessions.ContainsKey(server.Id))
			.ToDictionary(server => server.Id, _ => 0);
	}

	public async Task RequestOnlineGameServerCharacterCountsAsync(int accountId)
	{
		foreach (var server in GetGameServers())
		{
			if (!server.IsOnline || !_onlineSessions.TryGetValue(server.Id, out var session))
				continue;

			await session.SendPacketAsync(new SmGameServerCharacterResponse(accountId));
		}
	}

	private static string ExtractIp(string remoteAddress)
	{
		if (string.IsNullOrWhiteSpace(remoteAddress))
			return string.Empty;
		var colon = remoteAddress.LastIndexOf(':');
		return colon > 0 ? remoteAddress[..colon] : remoteAddress;
	}
}
