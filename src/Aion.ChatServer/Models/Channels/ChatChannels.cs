using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Models.Channels;

public sealed class ChatChannels
{
	private readonly ConcurrentDictionary<int, Channel> _channels = new();
	private readonly ILogger<ChatChannels> _logger;

	public ChatChannels(ILogger<ChatChannels> logger)
	{
		_logger = logger;
	}

	public Channel? GetChannelById(int channelId)
	{
		return _channels.TryGetValue(channelId, out var channel) ? channel : null;
	}

	public Channel? GetOrCreate(ChatClient client, string identifier)
	{
		var parsed = ParseIdentifier(identifier);
		if (parsed == null)
			return null;

		var (channelType, gameServerId, race, channelMeta) = parsed.Value;
		if (client.Race != race && client.AccessLevel == 0)
		{
			_logger.LogWarning("{Client} requested channel of race: {Race}", client, race);
			return null;
		}

		foreach (var channel in _channels.Values)
		{
			if (channel.Matches(channelType, gameServerId, race, channelMeta))
				return channel;
		}

		var newChannel = AddChannel(channelType, gameServerId, race, channelMeta);
		if (newChannel is JobChannel jobChannel && !jobChannel.HasAliases)
			_logger.LogWarning("{Client} requested channel for unknown class: {Class}", client, channelMeta);

		return newChannel;
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
