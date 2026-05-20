using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.ChatServer.Tests;

public class ChatChannelsTests
{
	[Fact]
	public void GetOrCreate_ParsesJavaIdentifierAndReusesMatchingRegionChannel()
	{
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var client = new ChatClient(1, new byte[48], "account", "Daeva", Race.Elyos, accessLevel: 0);
		var identifier = "@\u0001public_ALL\u00011.0.AION.KOR";

		var first = channels.GetOrCreate(client, identifier);
		var second = channels.GetOrCreate(client, identifier);

		var region = Assert.IsType<RegionChannel>(first);
		Assert.Same(first, second);
		Assert.Equal(ChannelType.Region, region.ChannelType);
		Assert.Equal(Race.Elyos, region.Race);
		Assert.Equal("ALL", region.MapIdentifier);
	}

	[Fact]
	public void GetOrCreate_RejectsOtherRaceForNormalAccessClient()
	{
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var client = new ChatClient(1, new byte[48], "account", "Daeva", Race.Elyos, accessLevel: 0);

		var channel = channels.GetOrCreate(client, "@\u0001public_ALL\u00011.1.AION.KOR");

		Assert.Null(channel);
	}
}
