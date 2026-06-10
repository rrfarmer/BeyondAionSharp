namespace Aion.GameServer.Utils;

public static class ChatUtil
{
	public static string? L10n(int l10nId)
	{
		// Java parity: utils/ChatUtil.l10n(int). Returns null for id 0.
		if (l10nId == 0)
			return null;
		var shifted = (l10nId << 1) | 1;
		return string.Concat("$", (char)(shifted & 0xffff), (char)((shifted >>> 16) & 0xffff));
	}

	public static string Color(string message, System.Drawing.Color? color)
	{
		// Java parity: utils/ChatUtil.color(String, java.awt.Color). null defaults to WHITE.
		System.Drawing.Color c = color ?? System.Drawing.Color.White;
		return Color(message, c.R, c.G, c.B);
	}

	public static string Color(string message, int rgb)
	{
		// Java parity: utils/ChatUtil.color(String, int).
		// Extracts R, G, B components from packed int.
		int r = (rgb & 0xFF0000) >> 16;
		int g = (rgb & 0xFF00) >> 8;
		int b = rgb & 0xFF;
		return Color(message, r, g, b);
	}

	public static string Color(string message, int r, int g, int b)
	{
		// Java parity: utils/ChatUtil.color(String, int, int, int).
		// Java uses DecimalFormat(".##") which shows up to 2 decimal places,
		// with no leading zero for values < 1 (e.g. 0.5 → ".5", 1.0 → "1.").
		// C# approximation: format as up to 2 decimal places, strip leading "0" if < 1.
		var rf = FormatColorComponent(r / 255f);
		var gf = FormatColorComponent(g / 255f);
		var bf = FormatColorComponent(b / 255f);
		return $"[color:{message};{rf} {gf} {bf}]";
	}

	public static string Genderize(string wordForMales, string textForFemales)
	{
		// Java parity: utils/ChatUtil.genderize(String, String).
		return $"{wordForMales}[f:\"{textForFemales}\"]";
	}

	// Java parity: utils/ChatUtil.name(Player) — see Name(String).
	public static string Name(Aion.GameServer.Model.GameObjects.Player.Player player)
	{
		return Name(player.GetName(true));
	}

	public static string Name(string name)
	{
		// Java parity: utils/ChatUtil.name(String). Returns a clickable character name link.
		return $"[charname:{name};1 1 1]";
	}

	private static string FormatColorComponent(float value)
	{
		// Java DecimalFormat(".##"): up to 2 decimal places, no leading zero for values < 1.
		// e.g. 0.502 → ".5", 1.0 → "1.", 0.0 → "0." (Java shows "0." for exact zero)
		var formatted = value.ToString(".##", System.Globalization.CultureInfo.InvariantCulture);
		return formatted == string.Empty ? "0." : formatted;
	}
}
