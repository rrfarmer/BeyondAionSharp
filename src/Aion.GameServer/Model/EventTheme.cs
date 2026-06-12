namespace Aion.GameServer.Model;

public enum EventTheme
{
	NONE = 0,
	CHRISTMAS = 1 << 0,
	HALLOWEEN = 1 << 1,
	VALENTINE = 1 << 2,
	BRAXCAFE = 1 << 3,
	TEST_BASIC_1 = 1 << 4,
	TEST_BASIC_2 = 1 << 5,
	TEST_BASIC_3 = 1 << 6,
	TEST_BASIC_4 = 1 << 7
}

public static class EventThemeExtensions
{
	public static int GetId(this EventTheme theme)
	{
		// Java parity: model/EventTheme.getId returns the constructor id for SM_VERSION_CHECK SceneStatus.
		return (int)theme;
	}
}
