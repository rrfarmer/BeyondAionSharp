namespace Aion.ChatServer.Models.Channels;

public sealed class RegionChannel : RaceChannel
{
	public RegionChannel(int gameServerId, Race race, string mapIdentifier)
		: base(ChannelType.Region, gameServerId, race)
	{
		MapIdentifier = mapIdentifier;
	}

	public string MapIdentifier { get; }

	public override bool Matches(ChannelType channelType, int gameServerId, Race race, string mapIdentifier)
	{
		return MapIdentifier == mapIdentifier && base.Matches(channelType, gameServerId, race, mapIdentifier);
	}
}
