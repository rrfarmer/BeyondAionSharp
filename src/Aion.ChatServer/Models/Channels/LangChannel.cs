namespace Aion.ChatServer.Models.Channels;

public sealed class LangChannel : RaceChannel
{
	public LangChannel(int gameServerId, Race race, string language)
		: base(ChannelType.Lang, gameServerId, race)
	{
		Language = language;
	}

	public string Language { get; }

	public override bool Matches(ChannelType channelType, int gameServerId, Race race, string language)
	{
		return Language == language && base.Matches(channelType, gameServerId, race, language);
	}

	public override string Name()
	{
		return $"{ChannelType.ToString().ToUpperInvariant()}: {Language} ({Race.ToString()[0]})";
	}
}
