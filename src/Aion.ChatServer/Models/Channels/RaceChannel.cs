namespace Aion.ChatServer.Models.Channels;

public abstract class RaceChannel : Channel
{
	protected RaceChannel(ChannelType channelType, int gameServerId, Race race)
		: base(channelType, gameServerId)
	{
		Race = race;
	}

	public Race Race { get; }

	public override bool Matches(ChannelType channelType, int gameServerId, Race race, string channelMeta)
	{
		return race == Race && channelType == ChannelType && gameServerId == GameServerId;
	}

	public override string Name()
	{
		return $"{ChannelType.ToString().ToUpperInvariant()} ({Race.ToString()[0]})";
	}
}
