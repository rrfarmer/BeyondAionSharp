namespace Aion.ChatServer.Models;

public enum ChannelType
{
	Region,
	Trade,
	Lfg,
	Job,
	Lang,
}

public static class ChannelTypeExtensions
{
	private static readonly IReadOnlyDictionary<string, ChannelType> TypesByIdentifier = new Dictionary<string, ChannelType>(StringComparer.Ordinal)
	{
		["public"] = ChannelType.Region,
		["trade"] = ChannelType.Trade,
		["partyFind"] = ChannelType.Lfg,
		["job"] = ChannelType.Job,
		["User"] = ChannelType.Lang,
	};

	public static string GetIdentifier(this ChannelType channelType)
	{
		return channelType switch
		{
			ChannelType.Region => "public",
			ChannelType.Trade => "trade",
			ChannelType.Lfg => "partyFind",
			ChannelType.Job => "job",
			ChannelType.Lang => "User",
			_ => throw new ArgumentOutOfRangeException(nameof(channelType), channelType, null)
		};
	}

	public static ChannelType? FromIdentifier(string identifier)
	{
		return TypesByIdentifier.TryGetValue(identifier, out var type) ? type : null;
	}
}
