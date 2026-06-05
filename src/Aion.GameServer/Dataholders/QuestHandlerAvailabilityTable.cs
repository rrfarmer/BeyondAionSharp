using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace Aion.GameServer.Dataholders;

public sealed class QuestHandlerAvailabilityTable
{
	private static readonly Regex PublicConcreteQuestHandlerPattern = new(
		@"public\s+class\s+[A-Za-z_][A-Za-z0-9_]*\s+extends\s+AbstractQuestHandler",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex AbstractClassPattern = new(
		@"(?:public\s+abstract|abstract\s+public|abstract)\s+class\s+[A-Za-z_][A-Za-z0-9_]*",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex SuperQuestIdPattern = new(
		@"\bsuper\s*\(\s*(?<questId>\d+)\s*\)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex IntAssignmentPattern = new(
		@"\bint\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>\d+)\s*;",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex SuperNamedQuestIdPattern = new(
		@"\bsuper\s*\(\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public QuestHandlerAvailabilityTable(IEnumerable<int> questIds)
	{
		var ids = questIds.ToHashSet();
		QuestIds = new ReadOnlySet<int>(ids);
	}

	public static QuestHandlerAvailabilityTable Empty { get; } = new(Array.Empty<int>());

	public IReadOnlySet<int> QuestIds { get; }

	public int Count => QuestIds.Count;

	public bool IsHaveHandler(int questId)
	{
		// Java parity: questEngine/QuestEngine.isHaveHandler checks the loaded handler map by quest id.
		return QuestIds.Contains(questId);
	}

	public static QuestHandlerAvailabilityTable Load(
		string cacheFilePath,
		string? javaHandlerDirectory,
		CancellationToken cancellationToken = default)
	{
		var questIds = new HashSet<int>(LoadXmlQuestIds(cacheFilePath));
		if (!string.IsNullOrWhiteSpace(javaHandlerDirectory) && Directory.Exists(javaHandlerDirectory))
		{
			foreach (var filePath in Directory.EnumerateFiles(javaHandlerDirectory, "*.java", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
			{
				cancellationToken.ThrowIfCancellationRequested();
				var source = File.ReadAllText(filePath);
				if (TryReadJavaHandlerQuestId(source, out var questId))
					questIds.Add(questId);
			}
		}

		return new QuestHandlerAvailabilityTable(questIds);
	}

	public static bool TryReadJavaHandlerQuestId(string javaSource, out int questId)
	{
		ArgumentNullException.ThrowIfNull(javaSource);

		if (!PublicConcreteQuestHandlerPattern.IsMatch(javaSource) || AbstractClassPattern.IsMatch(javaSource))
		{
			questId = 0;
			return false;
		}

		var literalMatch = SuperQuestIdPattern.Match(javaSource);
		if (literalMatch.Success)
		{
			questId = int.Parse(literalMatch.Groups["questId"].Value, CultureInfo.InvariantCulture);
			return true;
		}

		var namedMatch = SuperNamedQuestIdPattern.Match(javaSource);
		if (namedMatch.Success)
		{
			var constants = ReadScalarIntAssignments(javaSource);
			if (constants.TryGetValue(namedMatch.Groups["name"].Value, out questId))
				return true;
		}

		questId = 0;
		return false;
	}

	private static IReadOnlySet<int> LoadXmlQuestIds(string cacheFilePath)
	{
		// Java parity: QuestEngine.init registers every DataManager.XML_QUESTS.getAllQuests() entry.
		var ids = new HashSet<int>();
		if (!File.Exists(cacheFilePath))
			return ids;

		var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit };
		using var reader = XmlReader.Create(cacheFilePath, settings);
		var questScriptsDepth = -1;
		while (reader.Read())
		{
			if (reader.NodeType == XmlNodeType.Element
				&& string.Equals(reader.LocalName, "quest_scripts", StringComparison.Ordinal))
			{
				questScriptsDepth = reader.Depth;
				continue;
			}

			if (reader.NodeType == XmlNodeType.EndElement
				&& questScriptsDepth >= 0
				&& reader.Depth == questScriptsDepth
				&& string.Equals(reader.LocalName, "quest_scripts", StringComparison.Ordinal))
			{
				questScriptsDepth = -1;
				continue;
			}

			if (reader.NodeType != XmlNodeType.Element
				|| questScriptsDepth < 0
				|| reader.Depth <= questScriptsDepth
				|| !reader.MoveToAttribute("id"))
				continue;

			if (int.TryParse(reader.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var questId))
				ids.Add(questId);

			reader.MoveToElement();
		}

		return ids;
	}

	private static Dictionary<string, int> ReadScalarIntAssignments(string javaSource)
	{
		var values = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (Match match in IntAssignmentPattern.Matches(javaSource))
			values[match.Groups["name"].Value] = int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);

		return values;
	}
}
