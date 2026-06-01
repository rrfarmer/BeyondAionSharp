namespace Aion.GameServer.Model;

public enum EventTheme
{
	None = 0,
	Christmas = 1 << 0,
	Halloween = 1 << 1,
	Valentine = 1 << 2,
	Braxcafe = 1 << 3,
	TestBasic1 = 1 << 4,
	TestBasic2 = 1 << 5,
	TestBasic3 = 1 << 6,
	TestBasic4 = 1 << 7
}

public static class EventThemeExtensions
{
	public static int GetId(this EventTheme theme)
	{
		// Java parity: model/EventTheme.getId returns the constructor id for SM_VERSION_CHECK SceneStatus.
		return (int)theme;
	}
}
