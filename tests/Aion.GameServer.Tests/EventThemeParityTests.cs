using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.Services.Event;

namespace Aion.GameServer.Tests;

public sealed class EventThemeParityTests
{
	[Fact]
	public void ThemeSelection_SkipsAlwaysActiveThemeLessEvent()
	{
		var alwaysActive = new Event(new EventTemplate { Name = "Always active", Theme = null });
		var seasonal = new Event(new EventTemplate { Name = "Seasonal", Theme = EventTheme.CHRISTMAS });

		var selected = EventService.SelectEventTheme(new[] { alwaysActive, seasonal });

		Assert.Equal(EventTheme.CHRISTMAS, selected);
	}

	[Fact]
	public void ThemeSelection_UsesNoneWhenNoActiveEventHasATheme()
	{
		var selected = EventService.SelectEventTheme(
			new[] { new Event(new EventTemplate { Name = "Always active", Theme = null }) });

		Assert.Equal(EventTheme.NONE, selected);
	}
}
