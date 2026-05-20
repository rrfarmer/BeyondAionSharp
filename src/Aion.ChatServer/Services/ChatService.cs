using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network.Handlers;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Services;

public sealed class ChatService : IChatService
{
	private readonly ConcurrentDictionary<int, ChatClient> _players = new();
	private readonly ChatChannels _channels;
	private readonly IBroadcastService _broadcastService;
	private readonly ILogger<ChatService> _logger;

	public ChatService(ChatChannels channels, IBroadcastService broadcastService, ILogger<ChatService> logger)
	{
		_channels = channels;
		_broadcastService = broadcastService;
		_logger = logger;
	}

	public ChatClient RegisterPlayer(int playerId, string accountName, string nick, Race race, byte accessLevel)
	{
		var accountToken = ComputeJavaAccountToken(accountName);
		var token = GenerateToken(accountToken);
		var chatClient = new ChatClient(playerId, token, accountName, nick, race, accessLevel);
		_players[playerId] = chatClient;
		return chatClient;
	}

	public ChatClient? GetPlayer(int playerId)
	{
		return _players.TryGetValue(playerId, out var player) ? player : null;
	}

	public bool RegisterPlayerConnection(int playerId, byte[] token, byte[] identifier, string name, string accountName, IChatClientConnection connection)
	{
		var chatClient = GetPlayer(playerId);
		if (chatClient == null)
		{
			_logger.LogWarning("Client tried to connect but was not yet registered from game server side");
			return false;
		}

		if (!chatClient.Token.SequenceEqual(token))
		{
			_logger.LogWarning("Client tried to connect but given token does not match");
			return false;
		}

		if (!string.Equals(chatClient.AccountName, accountName, StringComparison.OrdinalIgnoreCase))
		{
			_logger.LogWarning("Client tried to connect with account name: {AccountName} expected: {Expected}", accountName, chatClient.AccountName);
			return false;
		}

		if (!string.Equals(chatClient.Name, name, StringComparison.Ordinal))
		{
			_logger.LogWarning("Client tried to connect with character name: {Name} expected: {Expected}", name, chatClient.Name);
			return false;
		}

		chatClient.AttachConnection(identifier, connection);
		_broadcastService.AddClient(chatClient);
		return true;
	}

	public Channel? RegisterPlayerWithChannel(ChatClient client, int channelRequestId, string identifier)
	{
		var channel = _channels.GetOrCreate(client, identifier);
		if (channel == null)
			return null;

		client.AddChannel(channel);
		_logger.LogDebug("Registered {Client} with channel {ChannelId} for request {RequestId}", client, channel.ChannelId, channelRequestId);
		return channel;
	}

	public ChatClient? PlayerLogout(int playerId)
	{
		if (!_players.TryRemove(playerId, out var chatClient))
			return null;

		_broadcastService.RemoveClient(chatClient);
		_logger.LogInformation("Player[id={PlayerId}] logged out", playerId);
		return chatClient;
	}

	public void GagPlayer(int playerId, long gagTimeMillis)
	{
		var client = GetPlayer(playerId);
		if (client == null)
			return;

		client.SetGagTime(gagTimeMillis);
		_logger.LogInformation("Player[id={PlayerId}] was gagged for {Minutes} minutes", playerId, gagTimeMillis / 60000);
	}

	private static byte[] ComputeJavaAccountToken(string accountName)
	{
		var bytes = Encoding.UTF8.GetBytes(accountName);
		var javaLength = Math.Min(accountName.Length, bytes.Length);
		return SHA256.HashData(bytes.AsSpan(0, javaLength));
	}

	private static byte[] GenerateToken(byte[] accountToken)
	{
		var dynamicToken = new byte[16];
		RandomNumberGenerator.Fill(dynamicToken);

		var token = new byte[48];
		Buffer.BlockCopy(dynamicToken, 0, token, 0, dynamicToken.Length);
		Buffer.BlockCopy(accountToken, 0, token, dynamicToken.Length, accountToken.Length);
		return token;
	}
}
