using Aion.GameServer.Utils.Extensions;

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

	/// <summary>Java parity: utils/ChatUtil.path(VisibleObject, boolean) — spawn-point link for an object.</summary>
	public static string Path(Aion.GameServer.Model.GameObjects.VisibleObject obj, bool withIdInName)
	{
		return Path(obj.GetObjectTemplate(), withIdInName);
	}

	/// <summary>Java parity: utils/ChatUtil.path(int npcId, boolean).</summary>
	public static string Path(int npcId, bool withIdInName)
	{
		Aion.GameServer.Model.Templates.VisibleObjectTemplate template = Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(npcId);
		if (template != null)
			return Path(template, withIdInName);
		else
			return Path(withIdInName ? "Unknown ID " + npcId : "Unknown ID", npcId);
	}

	// Java parity: utils/ChatUtil.path(VisibleObjectTemplate, boolean).
	private static string Path(Aion.GameServer.Model.Templates.VisibleObjectTemplate template, bool withIdInName)
	{
		string name = template.GetL10n();
		if (name == null)
			name = Capitalize(template.GetName());
		if (withIdInName)
			name = name + " | " + template.GetTemplateId();
		return Path(name, template.GetTemplateId());
	}

	/// <summary>Java parity: utils/ChatUtil.path(String npcName) — spawn-point link by localized name.</summary>
	public static string Path(string npcName)
	{
		return string.Format("[where:{0}]", npcName);
	}

	/// <summary>Java parity: utils/ChatUtil.path(String linkName, int npcId) — spawn-point link by ID.</summary>
	public static string Path(string linkName, int npcId)
	{
		return string.Format("[where:{0};{1}]", linkName, npcId);
	}

	// Java parity: utils/ChatUtil.position(String, WorldPosition).
	public static string Position(string label, Aion.GameServer.World.WorldPosition pos)
	{
		return Position(label, pos.GetMapId(), pos.GetX(), pos.GetY(), pos.GetZ());
	}

	// Java parity: utils/ChatUtil.position(String, long, float, float, float).
	public static string Position(string label, long worldId, float x, float y, float z)
	{
		byte subId = 0;
		if (worldId == 400010000) // abyss map
		{
			if (z > 1800 && z < 2800 && x > 1600 && x < 2700 && y > 1400 && y < 2500)
				subId = 2; // core
			else if (z > 2250)
				subId = 3; // upper
			else
				subId = 1; // lower
		}
		return string.Format("[pos:{0};{1} {2} {3} {4} {5}]", label, worldId, Fmt(x), Fmt(y), Fmt(z), subId);
	}

	// Java parity: DecimalFormat.getInstance(Locale.US) — grouping comma, max 3 fraction digits, min 1 integer digit.
	private static string Fmt(float v)
	{
		return v.ToString("#,##0.###", System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary>Java parity: utils/ChatUtil.getPosition(String). Parses an Aion "[pos:...]" link. Returns null if input was invalid.</summary>
	public static Aion.GameServer.World.WorldPosition GetPosition(string posLink)
	{
		if (posLink == null || !posLink.StartsWith("[pos:"))
			return null;

		int startIndex = posLink.IndexOf(";");
		int endIndex = posLink.IndexOf("]");
		if (startIndex < 0 || startIndex >= endIndex)
			return null;
		string[] posStr = System.Text.RegularExpressions.Regex.Split(posLink.Substring(startIndex, endIndex - startIndex).Trim(), "\\s+")
			.Where(s => s.Length > 0).ToArray();
		if (ToInt(posStr[0]) <= 1) // if present, strip ely/asmo language restriction flag (0 = ely only, 1 = asmo only)
			posStr = posStr.Skip(1).ToArray();

		if (posStr.Length < 3)
			return null;

		int mapAndInstanceId = ToInt(posStr[0]);
		float x = ToFloat(posStr[1]);
		float y = ToFloat(posStr[2]);
		float z = posStr.Length > 3 ? ToFloat(posStr[3]) : 0; // client always creates position links with z = 0
		int layer = posStr.Length > 4 ? ToInt(posStr[4]) : 0;
		int? zSearchOffset = null;
		if (layer > 0 && z == 0 && mapAndInstanceId == 400010000) // abyss
		{
			switch (layer)
			{
				case 1: // lower
					z = 1700f;
					break;
				case 2: // core
					z = 2350f;
					break;
				case 3: // upper
					z = 2950f;
					break;
			}
			zSearchOffset = -250;
		}

		return ParsedCoordsToWorldPosition(mapAndInstanceId, x, y, z == 0 ? (float?)null : z, zSearchOffset);
	}

	// Java parity: org.apache.commons.lang3.math.NumberUtils.toInt(String).
	private static int ToInt(string s)
	{
		return int.TryParse(s, out var r) ? r : 0;
	}

	// Java parity: org.apache.commons.lang3.math.NumberUtils.toFloat(String).
	private static float ToFloat(string s)
	{
		return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0f;
	}

	// Java parity: utils/ChatUtil.parsedCoordsToWorldPosition(int, float, float, Float, Integer).
	public static Aion.GameServer.World.WorldPosition ParsedCoordsToWorldPosition(int mapAndInstanceId, float x, float y, float? z, int? zSearchOffset)
	{
		int instanceId = mapAndInstanceId % 10000;
		int mapId = mapAndInstanceId;
		if (instanceId > 0)
			mapId -= instanceId;
		Aion.GameServer.World.WorldMap map = Aion.GameServer.World.World.GetInstance().GetWorldMap(mapId);
		if (map == null)
			return null;
		instanceId += 1; // client counts instanceIds starting at 0, but on server side it starts at 1 (TODO change server side)
		if (instanceId > 1 && !map.GetAvailableInstanceIds().Contains(instanceId))
			instanceId = 1;
		float geoZ;
		if (z == null)
			geoZ = Aion.GameServer.World.Geo.GeoService.GetInstance().GetZ(mapId, x, y, 4000, 0, instanceId);
		else if (zSearchOffset == null)
			geoZ = Aion.GameServer.World.Geo.GeoService.GetInstance().GetZ(mapId, x, y, z.Value, instanceId); // search relative to input z (max diff = z ±2)
		else
			geoZ = Aion.GameServer.World.Geo.GeoService.GetInstance().GetZ(mapId, x, y, z.Value, z.Value + zSearchOffset.Value, instanceId);

		if (!float.IsNaN(geoZ))
			z = geoZ;

		return z == null ? null : Aion.GameServer.World.World.GetInstance().CreatePosition(mapId, x, y, z.Value, (byte)0, instanceId);
	}

	// Java parity: org.apache.commons.lang3.StringUtils.capitalize — capitalizes the first character.
	public static string Capitalize(string str)
	{
		if (string.IsNullOrEmpty(str))
			return str;
		return char.ToUpper(str[0]) + str.Substring(1);
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
