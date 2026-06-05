using System.Xml;
using System.Xml.Schema;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Dataholders.LoadingUtils;

public sealed class XmlDataLoaderOptions
{
	public required string MainXmlFilePath { get; init; }

	public required string CacheXmlFilePath { get; init; }

	public required string SchemaFilePath { get; init; }

	public string? QuestHandlerDirectory { get; init; }

	public bool ValidateWhenCacheChanges { get; init; } = true;
}

public static class XmlDataLoader
{
	public static Task<StaticData> LoadStaticDataAsync(XmlDataLoaderOptions options, ILogger? logger = null, CancellationToken cancellationToken = default)
	{
		// Java parity: DataManager static data cache load and optional background validation.
		var merger = new XmlMerger(options.MainXmlFilePath, options.CacheXmlFilePath);
		var mergeResult = merger.Merge();
		Task? validationTask = null;
		if (mergeResult.FileWasModified && options.ValidateWhenCacheChanges)
		{
			validationTask = Task.Run(
				() =>
				{
					logger?.LogInformation("Validating {CacheFile} in background", mergeResult.CacheFilePath);
					ValidateCache(mergeResult.CacheFilePath, options.SchemaFilePath);
				},
				CancellationToken.None);
		}

		return StaticData.LoadFromCacheAsync(
			mergeResult.CacheFilePath,
			mergeResult.ImportedFiles,
			options.QuestHandlerDirectory,
			validationTask,
			cancellationToken);
	}

	public static void ValidateCache(string cacheFilePath, string schemaFilePath)
	{
		// Java parity: static-data XSD validation of generated cache/static_data.xml.
		var schemas = new XmlSchemaSet();
		schemas.Add(null, schemaFilePath);

		var settings = new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			ValidationType = ValidationType.Schema,
			Schemas = schemas,
		};

		using var reader = XmlReader.Create(cacheFilePath, settings);
		while (reader.Read())
		{
		}
	}
}
