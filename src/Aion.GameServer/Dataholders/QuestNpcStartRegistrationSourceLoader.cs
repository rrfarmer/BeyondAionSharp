namespace Aion.GameServer.Dataholders;

public sealed class QuestNpcStartRegistrationSourceLoader
{
	private readonly QuestNpcStartXmlExtractor _xmlExtractor;
	private readonly QuestNpcStartJavaHandlerExtractor _javaHandlerExtractor;

	public QuestNpcStartRegistrationSourceLoader()
		: this(new QuestNpcStartXmlExtractor(), new QuestNpcStartJavaHandlerExtractor())
	{
	}

	public QuestNpcStartRegistrationSourceLoader(
		QuestNpcStartXmlExtractor xmlExtractor,
		QuestNpcStartJavaHandlerExtractor javaHandlerExtractor)
	{
		_xmlExtractor = xmlExtractor;
		_javaHandlerExtractor = javaHandlerExtractor;
	}

	public QuestNpcStartRegistrationSourceLoadResult Load(
		string? questScriptDirectory,
		string? javaHandlerDirectory,
		CancellationToken cancellationToken = default)
	{
		var sources = new List<QuestNpcStartRegistrationSource>();
		var unresolved = new List<QuestNpcStartJavaHandlerUnresolvedRegistration>();

		if (!string.IsNullOrWhiteSpace(questScriptDirectory))
		{
			foreach (var filePath in EnumerateFiles(questScriptDirectory, "*.xml", cancellationToken))
			{
				using var stream = File.OpenRead(filePath);
				sources.AddRange(_xmlExtractor.Extract(stream, NormalizePath(filePath)));
			}
		}

		if (!string.IsNullOrWhiteSpace(javaHandlerDirectory))
		{
			foreach (var filePath in EnumerateFiles(javaHandlerDirectory, "*.java", cancellationToken))
			{
				var source = File.ReadAllText(filePath);
				var result = _javaHandlerExtractor.Extract(source, NormalizePath(filePath));
				sources.AddRange(result.Sources);
				unresolved.AddRange(result.Unresolved);
			}
		}

		return new QuestNpcStartRegistrationSourceLoadResult(sources, unresolved);
	}

	private static IEnumerable<string> EnumerateFiles(string directory, string searchPattern, CancellationToken cancellationToken)
	{
		if (!Directory.Exists(directory))
			yield break;

		foreach (var filePath in Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories).Order(StringComparer.Ordinal))
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return filePath;
		}
	}

	private static string NormalizePath(string filePath)
	{
		return filePath.Replace(Path.DirectorySeparatorChar, '/');
	}
}

public sealed record QuestNpcStartRegistrationSourceLoadResult(
	IReadOnlyList<QuestNpcStartRegistrationSource> Sources,
	IReadOnlyList<QuestNpcStartJavaHandlerUnresolvedRegistration> Unresolved);
