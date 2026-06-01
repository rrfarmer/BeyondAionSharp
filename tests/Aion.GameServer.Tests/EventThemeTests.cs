using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

public sealed class EventThemeTests
{
	[Theory]
	[InlineData(EventTheme.None, 0)]
	[InlineData(EventTheme.Christmas, 1)]
	[InlineData(EventTheme.Halloween, 2)]
	[InlineData(EventTheme.Valentine, 4)]
	[InlineData(EventTheme.Braxcafe, 8)]
	[InlineData(EventTheme.TestBasic1, 16)]
	[InlineData(EventTheme.TestBasic2, 32)]
	[InlineData(EventTheme.TestBasic3, 64)]
	[InlineData(EventTheme.TestBasic4, 128)]
	public void GetId_ReturnsJavaEventThemeIds(EventTheme theme, int expectedId)
	{
		Assert.Equal(expectedId, theme.GetId());
	}
}
