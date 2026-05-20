using System.Collections.Concurrent;

namespace Aion.ChatServer.Models.Channels;

public abstract class Channel
{
	private static int _nextId;
	private readonly ConcurrentDictionary<int, ChatClient> _members = new();

	protected Channel(ChannelType channelType, int gameServerId)
	{
		ChannelType = channelType;
		GameServerId = gameServerId;
		ChannelId = Interlocked.Increment(ref _nextId);
	}

	public ChannelType ChannelType { get; }

	public int GameServerId { get; }

	public int ChannelId { get; }

	public IReadOnlyCollection<ChatClient> Members => _members.Values.ToArray();

	public void AddMember(ChatClient client)
	{
		_members[client.ClientId] = client;
	}

	public void RemoveMember(int clientId)
	{
		_members.TryRemove(clientId, out _);
	}

	public abstract bool Matches(ChannelType channelType, int gameServerId, Race race, string channelMeta);

	public abstract string Name();
}
