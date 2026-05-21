namespace Aion.GameServer.Utils;

public static class ChatUtil
{
	public static string L10n(int l10nId)
	{
		// Java parity: utils/ChatUtil.l10n(int).
		var shifted = (l10nId << 1) | 1;
		return string.Concat("$", (char)(shifted & 0xffff), (char)((shifted >>> 16) & 0xffff));
	}
}
