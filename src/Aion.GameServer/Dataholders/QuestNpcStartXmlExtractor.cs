using System.Globalization;
using System.Xml;

namespace Aion.GameServer.Dataholders;

public sealed class QuestNpcStartXmlExtractor
{
	public IReadOnlyList<QuestNpcStartRegistrationSource> Extract(string xmlContent, string sourcePath)
	{
		ArgumentNullException.ThrowIfNull(xmlContent);
		using var reader = XmlReader.Create(new StringReader(xmlContent), CreateSettings());
		return Extract(reader, sourcePath);
	}

	public IReadOnlyList<QuestNpcStartRegistrationSource> Extract(Stream stream, string sourcePath)
	{
		ArgumentNullException.ThrowIfNull(stream);
		using var reader = XmlReader.Create(stream, CreateSettings());
		return Extract(reader, sourcePath);
	}

	private static IReadOnlyList<QuestNpcStartRegistrationSource> Extract(XmlReader reader, string sourcePath)
	{
		ArgumentNullException.ThrowIfNull(sourcePath);

		var sources = new List<QuestNpcStartRegistrationSource>();
		while (reader.Read())
		{
			if (reader.NodeType != XmlNodeType.Element)
				continue;

			var startNpcIds = reader.GetAttribute("start_npc_ids");
			if (string.IsNullOrWhiteSpace(startNpcIds))
				continue;

			var questId = ReadRequiredIntAttribute(reader, "id", sourcePath);
			if (SuppressesNpcStartRegistration(reader, sourcePath))
				continue;

			foreach (var npcId in ReadIntListAttribute(startNpcIds, "start_npc_ids", sourcePath, reader.Name))
			{
				// Java parity: XMLQuest template register methods call QuestNpc.addOnQuestStart for start_npc_ids.
				sources.Add(new QuestNpcStartRegistrationSource(
					NpcId: npcId,
					QuestId: questId,
					SourceKind: QuestNpcStartRegistrationSourceKind.XmlQuest,
					SourcePath: sourcePath));
			}
		}

		return sources;
	}

	private static bool SuppressesNpcStartRegistration(XmlReader reader, string sourcePath)
	{
		if (!reader.Name.Equals("report_to_many", StringComparison.Ordinal))
			return false;

		var startItemId = reader.GetAttribute("start_item_id");
		if (string.IsNullOrWhiteSpace(startItemId))
			return false;

		// Java parity: ReportToMany.register uses registerQuestItem when startItemId != 0.
		return ReadInt(startItemId, "start_item_id", sourcePath, reader.Name) != 0;
	}

	private static IReadOnlyList<int> ReadIntListAttribute(string value, string attributeName, string sourcePath, string elementName)
	{
		var tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var values = new List<int>(tokens.Length);
		foreach (var token in tokens)
			values.Add(ReadInt(token, attributeName, sourcePath, elementName));
		return values;
	}

	private static int ReadRequiredIntAttribute(XmlReader reader, string attributeName, string sourcePath)
	{
		var value = reader.GetAttribute(attributeName);
		if (string.IsNullOrWhiteSpace(value))
			throw new FormatException($"Missing required '{attributeName}' attribute on '{reader.Name}' in {sourcePath}.");

		return ReadInt(value, attributeName, sourcePath, reader.Name);
	}

	private static int ReadInt(string value, string attributeName, string sourcePath, string elementName)
	{
		if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
			throw new FormatException($"Invalid integer '{value}' in '{attributeName}' on '{elementName}' in {sourcePath}.");

		return parsed;
	}

	private static XmlReaderSettings CreateSettings()
	{
		return new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			IgnoreComments = true,
			IgnoreWhitespace = true,
		};
	}
}
