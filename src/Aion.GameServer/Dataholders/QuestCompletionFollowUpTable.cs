using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Aion.GameServer.Dataholders;

public sealed class QuestCompletionFollowUpTable
{
	private readonly IReadOnlyList<QuestCompletionFollowUpRegistration> _registrations;

	public QuestCompletionFollowUpTable(IEnumerable<QuestCompletionFollowUpRegistration> registrations)
	{
		_registrations = registrations.ToArray();
	}

	public static QuestCompletionFollowUpTable Empty { get; } = new(Array.Empty<QuestCompletionFollowUpRegistration>());

	public int Count => _registrations.Count;

	public IReadOnlyList<QuestCompletionFollowUpRegistration> Registrations => _registrations;

	public IReadOnlyList<QuestCompletionFollowUpRegistration> GetDefaultFollowUps()
	{
		return _registrations;
	}

	public static QuestCompletionFollowUpTable Load(string? javaHandlerDirectory, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(javaHandlerDirectory) || !Directory.Exists(javaHandlerDirectory))
			return Empty;

		var registrations = new List<QuestCompletionFollowUpRegistration>();
		var extractor = new QuestCompletionFollowUpJavaHandlerExtractor();
		foreach (var filePath in Directory.EnumerateFiles(javaHandlerDirectory, "*.java", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
		{
			cancellationToken.ThrowIfCancellationRequested();
			var source = File.ReadAllText(filePath);
			registrations.AddRange(extractor.Extract(source, NormalizePath(filePath)).Registrations);
		}

		return new QuestCompletionFollowUpTable(registrations);
	}

	private static string NormalizePath(string filePath)
	{
		return filePath.Replace(Path.DirectorySeparatorChar, '/');
	}
}

public sealed record QuestCompletionFollowUpRegistration(
	int QuestId,
	IReadOnlyList<int> PreQuestIds,
	string SourcePath)
{
	public IReadOnlyList<int> PreQuestIds { get; } = new ReadOnlyCollection<int>(PreQuestIds.ToArray());
}

public sealed class QuestCompletionFollowUpJavaHandlerExtractor
{
	private static readonly Regex RegisterOnCompletedQuestIdPattern = new(
		@"registerOnQuestCompleted\s*\(\s*questId\s*\)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex DefaultFollowUpPattern = new(
		@"defaultOnQuestCompletedEvent\s*\(\s*env\s*(?:,\s*(?<args>[^)]*?))?\)",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

	private static readonly Regex IntArrayAssignmentPattern = new(
		@"\bint\s*\[\]\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*\{(?<values>[^}]*)\}\s*;",
		RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

	public QuestCompletionFollowUpJavaHandlerExtractionResult Extract(string javaSource, string sourcePath)
	{
		ArgumentNullException.ThrowIfNull(javaSource);
		ArgumentNullException.ThrowIfNull(sourcePath);

		if (!RegisterOnCompletedQuestIdPattern.IsMatch(javaSource)
			|| !QuestHandlerAvailabilityTable.TryReadJavaHandlerQuestId(javaSource, out var questId))
		{
			return new QuestCompletionFollowUpJavaHandlerExtractionResult([]);
		}

		var arrayConstants = ReadIntArrayAssignments(javaSource);
		var registrations = new List<QuestCompletionFollowUpRegistration>();
		foreach (Match match in DefaultFollowUpPattern.Matches(javaSource))
		{
			var args = match.Groups["args"].Success
				? match.Groups["args"].Value.Trim()
				: string.Empty;
			if (!TryReadPreQuestIds(args, arrayConstants, out var preQuestIds))
				continue;

			// Java parity: Aion.GameServer.QuestEngine.QuestEngine.onQuestCompleted invokes registered handlers;
			// AbstractQuestHandler.defaultOnQuestCompletedEvent starts or locks this handler's quest.
			registrations.Add(new QuestCompletionFollowUpRegistration(questId, preQuestIds, sourcePath));
		}

		return new QuestCompletionFollowUpJavaHandlerExtractionResult(registrations);
	}

	private static bool TryReadPreQuestIds(
		string args,
		IReadOnlyDictionary<string, int[]> arrayConstants,
		out IReadOnlyList<int> preQuestIds)
	{
		if (string.IsNullOrWhiteSpace(args))
		{
			preQuestIds = [];
			return true;
		}

		if (arrayConstants.TryGetValue(args, out var arrayValues))
		{
			preQuestIds = arrayValues;
			return true;
		}

		var values = new List<int>();
		foreach (var token in args.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
			{
				preQuestIds = [];
				return false;
			}

			values.Add(value);
		}

		preQuestIds = values;
		return true;
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
}

public sealed record QuestCompletionFollowUpJavaHandlerExtractionResult(
	IReadOnlyList<QuestCompletionFollowUpRegistration> Registrations);
