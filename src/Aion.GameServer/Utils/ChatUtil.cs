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
	public static string Name(Aion.GameServer.Model.GameObjects.Players.Player player)
	{
		return Name(player.GetName(true));
	}

	public static string Name(string name)
	{
		// Java parity: utils/ChatUtil.name(String). Returns a clickable character name link.
		return $"[charname:{name};1 1 1]";
	}

	// Java parity: utils/ChatUtil.item(int).
	public static string Item(int itemId)
	{
		return $"[item:{itemId}]";
	}

	// Java parity: utils/ChatUtil.itemName(int).
	public static string ItemName(int itemId)
	{
		return $"[item_ex:{itemId}]";
	}

	// Java parity: utils/ChatUtil.recipe(int).
	public static string Recipe(int recipeId)
	{
		return $"[recipe:{recipeId}]";
	}

	// Java parity: utils/ChatUtil.quest(int).
	public static string Quest(int questId)
	{
		return $"[quest:{questId}]";
	}

	/// <summary>Java parity: utils/ChatUtil.getItemId(String). itemStr can be an ID string or Aion link like "[item: 110900785]". Returns 0 if no valid ID.</summary>
	public static int GetItemId(string itemStr)
	{
		return GetIdFromString(itemStr, "item", "1[0-9]{8}");
	}

	/// <summary>Java parity: utils/ChatUtil.getQuestId(String). questStr can be an ID string or Aion link like "[quest: 1006]". Returns 0 if no valid ID.</summary>
	public static int GetQuestId(string questStr)
	{
		return GetIdFromString(questStr, "quest", "[1-9][0-9]{3,4}");
	}

	private static int GetIdFromString(string input, string linkAccessor, string validationPattern)
	{
		// Java parity: utils/ChatUtil.getIdFromString(String, String, String).
		if (input == null)
			return 0;

		if (input.StartsWith("[" + linkAccessor + ":"))
			input = input.Substring(linkAccessor.Length + 2).Trim();

		System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(input, "^(" + validationPattern + ")(?:[^\\d][^\\[]*\\]?$|$)");
		if (m.Success)
			return int.TryParse(m.Groups[1].Value, out int result) ? result : 0;

		return 0;
	}

	private const char AsmoNamePrefix = '';
	private const char ElyosNamePrefix = '';

	/// <summary>Java parity: utils/ChatUtil.getRealCharName(String) — character name without custom tags.</summary>
	public static string GetRealCharName(string name)
	{
		return GetRealCharName(name, false);
	}

	// Java parity: utils/ChatUtil.getRealCharName(String, boolean).
	public static string GetRealCharName(string name, bool nameIsFromGMCommand)
	{
		// don't perform expensive checks if name is already qualified
		if (System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z]+$"))
			return Util.ConvertName(name);

		bool replaceUnsupportedCommandChars = nameIsFromGMCommand
			&& name.Contains(Aion.GameServer.Network.Aion.ClientPackets.AbstractGmCommandPacket.UNSUPPORTED_COMMAND_CHAR_PLACEHOLDER);
		char firstChar = name[0];
		if (firstChar == AsmoNamePrefix || firstChar == ElyosNamePrefix)
			name = name.Substring(1);

		const string nameFlag = "%s";
		foreach (string nameFormat in Aion.GameServer.Configs.Administration.AdminConfig.NAME_TAGS)
		{
			int nameStartIndex = nameFormat.IndexOf(nameFlag, StringComparison.Ordinal);
			if (nameStartIndex == -1)
				continue;
			string namePrefix = nameFormat.Substring(0, nameStartIndex > 0 ? nameStartIndex : 0);
			string nameSuffix = nameFormat.Substring(nameStartIndex + nameFlag.Length);
			if (replaceUnsupportedCommandChars)
			{
				namePrefix = Aion.GameServer.Network.Aion.ClientPackets.AbstractGmCommandPacket.ReplaceUnsupportedCommandChars(namePrefix);
				nameSuffix = Aion.GameServer.Network.Aion.ClientPackets.AbstractGmCommandPacket.ReplaceUnsupportedCommandChars(nameSuffix);
			}
			if ((namePrefix + nameSuffix).Length > 0 && name.StartsWith(namePrefix, StringComparison.Ordinal) && name.EndsWith(nameSuffix, StringComparison.Ordinal))
			{
				int endIndex = name.IndexOf(nameSuffix, StringComparison.Ordinal) - 1;
				name = name.Substring(namePrefix.Length, (endIndex > 0 ? endIndex : name.Length) - namePrefix.Length);
				break;
			}
		}

		return Util.ConvertName(name);
	}

	/// <summary>Java parity: utils/ChatUtil.toFactionPrefixedName(Player, Player) — player name prefixed with faction icon for staff readers.</summary>
	public static string ToFactionPrefixedName(Aion.GameServer.Model.GameObjects.Players.Player reader, Aion.GameServer.Model.GameObjects.Players.Player player)
	{
		string name = player.GetName(true);
		if (reader.IsStaff())
			name = (player.GetRace() == Aion.GameServer.Model.Race.ELYOS ? ElyosNamePrefix : AsmoNamePrefix) + name;
		return name;
	}

	private static string FormatColorComponent(float value)
	{
		// Java DecimalFormat(".##"): up to 2 decimal places, no leading zero for values < 1.
		// e.g. 0.502 → ".5", 1.0 → "1.", 0.0 → "0." (Java shows "0." for exact zero)
		var formatted = value.ToString(".##", System.Globalization.CultureInfo.InvariantCulture);
		return formatted == string.Empty ? "0." : formatted;
	}
}
