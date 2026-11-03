using System.Collections.Concurrent;
using Aion.ChatServer.Services;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Models.Channels;

public sealed class ChatChannels
{
	/// <summary>A channel name is a map, class or language name, so 64 characters is generous.</summary>
	private const int MaxMetaLength = 64;

	/// <summary>Every distinct name the client sends stays forever, and a real server needs a few dozen.</summary>
	private const int MaxChannels = 256;

	private readonly ConcurrentDictionary<int, Channel> _channels = new();
	private readonly object _creationLock = new();
	private readonly ILogger<ChatChannels> _logger;
	private readonly IGameServerService? _gameServerService;

	public ChatChannels(ILogger<ChatChannels> logger, IGameServerService? gameServerService = null)
	{
		_logger = logger;
		_gameServerService = gameServerService;
	}

	public Channel? GetChannelById(int channelId)
	{
		return _channels.TryGetValue(channelId, out var channel) ? channel : null;
	}

	public Channel? GetOrCreate(ChatClient client, string identifier)
	{
		var parsed = ParseIdentifier(identifier);
		if (parsed == null)
		{
			_logger.LogWarning("{Client} requested a malformed channel: {Identifier}", client, identifier);
			return null;
		}

		var (channelType, gameServerId, race, channelMeta) = parsed.Value;
		if (channelMeta.Length > MaxMetaLength)
		{
			_logger.LogWarning("{Client} requested a channel with a too long name: {Identifier}", client, identifier);
			return null;
		}

		// retail: 99 is forbidden and it must be this session's server
		if (gameServerId == 99 || _gameServerService?.GameServerId is byte ownId && gameServerId != ownId)
		{
			_logger.LogWarning("{Client} requested a channel of game server {GameServerId}: {Identifier}", client, gameServerId, identifier);
			return null;
		}

		if (client.Race != race && client.AccessLevel == 0)
		{
			_logger.LogWarning("{Client} requested channel of race: {Race}", client, race);
			return null;
		}

		// Java handles chat packets on one NIO read/write dispatcher, so its scan followed by add is effectively
		// serialized. C# runs client handlers concurrently and must preserve that invariant explicitly.
		lock (_creationLock)
		{
			foreach (var channel in _channels.Values)
			{
				if (channel.Matches(channelType, gameServerId, race, channelMeta))
					return channel;
			}

			if (_channels.Count >= MaxChannels)
			{
				_logger.LogWarning("{Client} requested a new channel, but {Count} already exist: {Identifier}", client, _channels.Count, identifier);
				return null;
			}

			var newChannel = AddChannel(channelType, gameServerId, race, channelMeta);
			if (newChannel is JobChannel jobChannel && !jobChannel.HasAliases)
				_logger.LogWarning("{Client} requested channel for unknown class: {Class}", client, channelMeta);

			return newChannel;
		}
	}

	public static (ChannelType ChannelType, int GameServerId, Race Race, string ChannelMeta)? ParseIdentifier(string identifier)
	{
		var parts = identifier.Split('\u0001');
		if (parts.Length != 3)
			return null;

		var channelTypeParts = parts[1].Split('_', 2);
		if (channelTypeParts.Length != 2)
			return null;

		var channelRestrictions = parts[2].Split('.');
		if (channelRestrictions.Length < 2)
			return null;

		var channelType = ChannelTypeExtensions.FromIdentifier(channelTypeParts[0]);
		if (channelType == null)
			return null;

		if (!int.TryParse(channelRestrictions[0], out var gameServerId))
			return null;

		if (!int.TryParse(channelRestrictions[1], out var raceId))
			return null;

		var race = RaceExtensions.FromId(raceId);
		if (race == null)
			return null;

		return (channelType.Value, gameServerId, race.Value, channelTypeParts[1]);
	}

	private Channel AddChannel(ChannelType channelType, int gameServerId, Race race, string channelMeta)
	{
		Channel channel = channelType switch
		{
			ChannelType.Region => new RegionChannel(gameServerId, race, channelMeta),
			ChannelType.Trade => new TradeChannel(gameServerId, race, channelMeta),
			ChannelType.Lfg => new LfgChannel(gameServerId, race),
			ChannelType.Job => new JobChannel(gameServerId, race, channelMeta),
			ChannelType.Lang => new LangChannel(gameServerId, race, channelMeta),
			_ => throw new ArgumentOutOfRangeException(nameof(channelType), channelType, null)
		};
		_channels[channel.ChannelId] = channel;
		return channel;
	}
}
