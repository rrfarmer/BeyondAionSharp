namespace Aion.ChatServer.Models.Channels;

public sealed class LfgChannel : RaceChannel
{
	public LfgChannel(int gameServerId, Race race)
		: base(ChannelType.Lfg, gameServerId, race)
	{
	}
}
