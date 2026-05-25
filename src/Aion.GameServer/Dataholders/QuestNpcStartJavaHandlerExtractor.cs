using System.Globalization;
using System.Text.RegularExpressions;

namespace Aion.GameServer.Dataholders;

public sealed class QuestNpcStartJavaHandlerExtractor
{
	private static readonly Regex StartRegistrationPattern = new(
		@"registerQuestNpc\s*\(\s*(?<npc>[^)]*?)\s*\)\s*\.\s*addOnQuestStart\s*\(\s*(?<quest>[^)]*?)\s*\)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex IntAssignmentPattern = new(
		@"\bint\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>\d+)\s*;",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex IntArrayAssignmentPattern = new(
		@"\bint\s*\[\]\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*\{(?<values>[^}]*)\}\s*;",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

	private static readonly Regex SuperQuestIdPattern = new(
		@"\bsuper\s*\(\s*(?<questId>\d+)\s*\)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex IndexedIdentifierPattern = new(
		@"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\[\s*(?<index>\d+)\s*\]$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public QuestNpcStartJavaHandlerExtractionResult Extract(string javaSource, string sourcePath)
	{
		ArgumentNullException.ThrowIfNull(javaSource);
		ArgumentNullException.ThrowIfNull(sourcePath);

		var scalarConstants = ReadScalarIntAssignments(javaSource);
		var arrayConstants = ReadIntArrayAssignments(javaSource);
		var inheritedQuestId = ReadInheritedQuestId(javaSource);
		var sources = new List<QuestNpcStartRegistrationSource>();
		var unresolved = new List<QuestNpcStartJavaHandlerUnresolvedRegistration>();

		foreach (Match match in StartRegistrationPattern.Matches(javaSource))
		{
			var npcExpression = match.Groups["npc"].Value.Trim();
			var questExpression = match.Groups["quest"].Value.Trim();
			var lineNumber = GetLineNumber(javaSource, match.Index);

			if (!TryResolveInt(npcExpression, scalarConstants, arrayConstants, out var npcId, out var npcReason))
			{
				unresolved.Add(new QuestNpcStartJavaHandlerUnresolvedRegistration(
					SourcePath: sourcePath,
					LineNumber: lineNumber,
					NpcExpression: npcExpression,
					QuestExpression: questExpression,
					Reason: npcReason));
				continue;
			}

			if (!TryResolveQuestId(questExpression, scalarConstants, arrayConstants, inheritedQuestId, out var questId, out var questReason))
			{
				unresolved.Add(new QuestNpcStartJavaHandlerUnresolvedRegistration(
					SourcePath: sourcePath,
					LineNumber: lineNumber,
					NpcExpression: npcExpression,
					QuestExpression: questExpression,
					Reason: questReason));
				continue;
			}

			// Java parity: handler register() methods add direct QuestNpc.onQuestStart registrations through QuestEngine.
			sources.Add(new QuestNpcStartRegistrationSource(
				NpcId: npcId,
				QuestId: questId,
				SourceKind: QuestNpcStartRegistrationSourceKind.JavaHandler,
				SourcePath: sourcePath));
		}

		return new QuestNpcStartJavaHandlerExtractionResult(sources, unresolved);
	}

	private static Dictionary<string, int> ReadScalarIntAssignments(string javaSource)
	{
		var values = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (Match match in IntAssignmentPattern.Matches(javaSource))
		{
			var name = match.Groups["name"].Value;
			var value = int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
			values[name] = value;
		}

		return values;
	}

	private static Dictionary<string, int[]> ReadIntArrayAssignments(string javaSource)
	{
		var values = new Dictionary<string, int[]>(StringComparer.Ordinal);
		foreach (Match match in IntArrayAssignmentPattern.Matches(javaSource))
		{
			var name = match.Groups["name"].Value;
			var tokens = match.Groups["values"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			var parsed = new List<int>(tokens.Length);
			foreach (var token in tokens)
			{
				if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
					parsed.Add(value);
			}

			values[name] = parsed.ToArray();
		}

		return values;
	}

	private static int? ReadInheritedQuestId(string javaSource)
	{
		var match = SuperQuestIdPattern.Match(javaSource);
		return match.Success
			? int.Parse(match.Groups["questId"].Value, CultureInfo.InvariantCulture)
			: null;
	}

	private static bool TryResolveQuestId(
		string expression,
		IReadOnlyDictionary<string, int> scalarConstants,
		IReadOnlyDictionary<string, int[]> arrayConstants,
		int? inheritedQuestId,
		out int value,
		out string reason)
	{
		if (expression.Equals("questId", StringComparison.Ordinal) && inheritedQuestId.HasValue)
		{
			value = inheritedQuestId.Value;
			reason = string.Empty;
			return true;
		}

		return TryResolveInt(expression, scalarConstants, arrayConstants, out value, out reason);
	}

	private static bool TryResolveInt(
		string expression,
		IReadOnlyDictionary<string, int> scalarConstants,
		IReadOnlyDictionary<string, int[]> arrayConstants,
		out int value,
		out string reason)
	{
		if (int.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
		{
			reason = string.Empty;
			return true;
		}

		if (scalarConstants.TryGetValue(expression, out value))
		{
			reason = string.Empty;
			return true;
		}

		var indexedMatch = IndexedIdentifierPattern.Match(expression);
		if (indexedMatch.Success)
		{
			var name = indexedMatch.Groups["name"].Value;
			var index = int.Parse(indexedMatch.Groups["index"].Value, CultureInfo.InvariantCulture);
			if (arrayConstants.TryGetValue(name, out var arrayValues) && index >= 0 && index < arrayValues.Length)
			{
				value = arrayValues[index];
				reason = string.Empty;
				return true;
			}
		}

		value = 0;
		reason = $"Unsupported expression '{expression}'.";
		return false;
	}

	private static int GetLineNumber(string source, int index)
	{
		var line = 1;
		for (var i = 0; i < index && i < source.Length; i++)
		{
			if (source[i] == '\n')
				line++;
		}

		return line;
	}
}

public sealed record QuestNpcStartJavaHandlerExtractionResult(
	IReadOnlyList<QuestNpcStartRegistrationSource> Sources,
	IReadOnlyList<QuestNpcStartJavaHandlerUnresolvedRegistration> Unresolved);

public sealed record QuestNpcStartJavaHandlerUnresolvedRegistration(
	string SourcePath,
	int LineNumber,
	string NpcExpression,
	string QuestExpression,
	string Reason);
