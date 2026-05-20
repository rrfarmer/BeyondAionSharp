using System.Collections.Concurrent;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network.Handlers;

namespace Aion.ChatServer.Models;

public sealed class ChatClient
{
	private readonly ConcurrentDictionary<ChannelType, List<Channel>> _channels = new();
	private readonly ConcurrentDictionary<ChannelType, long> _lastMessageTime = new();

	public ChatClient(int clientId, byte[] token, string accountName, string name, Race race, byte accessLevel)
	{
		ClientId = clientId;
		Token = token;
		AccountName = accountName;
		Name = name;
		Race = race;
		AccessLevel = accessLevel;
	}

	public int ClientId { get; }

	public byte[] Token { get; }

	public string AccountName { get; }

	public string Name { get; }

	public Race Race { get; }

	public byte AccessLevel { get; }

	public byte[]? Identifier { get; private set; }

	public IChatClientConnection? Connection { get; private set; }

	public long GagTime { get; private set; }

	public IReadOnlyDictionary<ChannelType, List<Channel>> Channels => _channels;

	public void AttachConnection(byte[] identifier, IChatClientConnection connection)
	{
		Identifier = identifier;
		Connection = connection;
	}

	public void AddChannel(Channel channel)
	{
		var channelsOfType = _channels.GetOrAdd(channel.ChannelType, _ => []);
		lock (channelsOfType)
		{
			if (channel.ChannelType != ChannelType.Job || channelsOfType.Count == 2)
				channelsOfType.Clear();
			channelsOfType.Add(channel);
		}
		channel.AddMember(this);
	}

	public bool RemoveChannel(Channel? channel)
	{
		if (channel == null)
			return false;

		if (!_channels.TryGetValue(channel.ChannelType, out var channelsOfType))
			return false;

		lock (channelsOfType)
		{
			var removed = channelsOfType.RemoveAll(ch => ch.ChannelId == channel.ChannelId) > 0;
			if (removed)
				channel.RemoveMember(ClientId);
			return removed;
		}
	}

	public bool IsInChannel(Channel channel)
	{
		if (!_channels.TryGetValue(channel.ChannelType, out var channelsOfType))
			return false;

		lock (channelsOfType)
			return channelsOfType.Any(ch => ch.ChannelId == channel.ChannelId);
	}

	public long GetLastMessageTime(ChannelType channelType)
	{
		return _lastMessageTime.GetValueOrDefault(channelType);
	}

	public void UpdateLastMessageTime(ChannelType channelType)
	{
		_lastMessageTime[channelType] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
	}

	public int NextMessageTimeSeconds(ChannelType channelType)
	{
		var delay = channelType is ChannelType.Lfg or ChannelType.Trade ? 30000 : 1000;
		var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - GetLastMessageTime(channelType);
		var floodProtectionTime = delay - elapsed;
		return floodProtectionTime <= 0 ? 0 : Math.Max(1, (int)(floodProtectionTime / 1000));
	}

	public bool IsGagged()
	{
		return GagTime > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < GagTime;
	}

	public void SetGagTime(long gagTime)
	{
		GagTime = gagTime;
	}

	public override string ToString()
	{
		return $"Player [name={Name}, id={ClientId}, race={Race}]";
	}
}
