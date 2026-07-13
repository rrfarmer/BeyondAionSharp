using System.Globalization;
using System.Xml;
using Aion.GameServer.Configs.Main;

namespace Aion.GameServer.Dataholders.LoadingUtils;

public sealed class XmlMerger
{
	private const string ImportElementName = "import";
	private const string FileAttributeName = "file";
	private const string SingleRootTagAttributeName = "singleRootTag";
	private const string RecursiveImportAttributeName = "recursiveImport";
	private const string MetadataCountKey = "__count";
	private static readonly IReadOnlyDictionary<int, string> CountryRegions = new Dictionary<int, string>
	{
		[1] = "usa",
		[2] = "europe",
		[4] = "japan",
		[5] = "china",
		[6] = "taiwan",
		[7] = "russia",
	};
	private static readonly uint[] CrcTable = BuildCrcTable();
	private readonly string _sourceFilePath;
	private readonly string _cacheFilePath;
	private readonly string _metadataFilePath;
	private readonly string _sourceDirectory;

	public XmlMerger(string sourceFilePath, string cacheFilePath)
	{
		_sourceFilePath = Path.GetFullPath(sourceFilePath);
		_cacheFilePath = Path.GetFullPath(cacheFilePath);
		_metadataFilePath = _cacheFilePath + ".properties";
		_sourceDirectory = Path.GetDirectoryName(_sourceFilePath) ?? throw new ArgumentException("Source file must have a parent directory.", nameof(sourceFilePath));
	}

	public XmlMergeResult Merge()
	{
		// Java parity: static_data.xml import expansion into cache/static_data.xml.
		if (!File.Exists(_sourceFilePath))
			throw new FileNotFoundException($"Source file {_sourceFilePath} not found.", _sourceFilePath);

		var importedFiles = CollectImportedFiles();
		if (IsCacheCurrent(importedFiles))
			return new XmlMergeResult(_cacheFilePath, fileWasModified: false, importedFiles);

		Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath)!);
		var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var settings = new XmlWriterSettings
		{
			Async = false,
			Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
			Indent = false,
		};

		using (var writer = XmlWriter.Create(_cacheFilePath, settings))
		{
			writer.WriteStartDocument();
			CopyWithImports(_sourceFilePath, writer, metadata);
			writer.WriteEndDocument();
		}

		metadata[MetadataCountKey] = importedFiles.Count.ToString(CultureInfo.InvariantCulture);
		WriteMetadata(metadata);
		return new XmlMergeResult(_cacheFilePath, fileWasModified: true, importedFiles);
	}

	private void CopyWithImports(string sourceFilePath, XmlWriter writer, IDictionary<string, string> metadata)
	{
		var settings = new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
		};

		using var reader = XmlReader.Create(sourceFilePath, settings);
		while (reader.Read())
		{
			if (reader.NodeType == XmlNodeType.Element && reader.LocalName == ImportElementName)
			{
				ProcessImport(reader, writer, metadata);
				continue;
			}

			if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == ImportElementName)
				continue;

			WriteCurrentNode(reader, writer);
		}
	}

	private void ProcessImport(XmlReader reader, XmlWriter writer, IDictionary<string, string> metadata)
	{
		// Java parity: data/static_data import attributes file, singleRootTag, recursiveImport.
		var importPath = reader.GetAttribute(FileAttributeName);
		if (string.IsNullOrWhiteSpace(importPath))
			throw new XmlException("Attribute 'file' is missing or empty on import element.");

		var fullPath = ApplyCountryOverride(Path.GetFullPath(Path.Combine(_sourceDirectory, importPath)));
		if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
			throw new FileNotFoundException($"Missing file to import: {fullPath}", fullPath);

		if (File.Exists(fullPath))
		{
			ImportFile(fullPath, skipStartElement: false, skipEndElement: false, writer, metadata);
			return;
		}

		var singleRootTag = bool.Parse(reader.GetAttribute(SingleRootTagAttributeName) ?? "false");
		var recursiveImport = bool.Parse(reader.GetAttribute(RecursiveImportAttributeName) ?? "true");
		var files = ListXmlFiles(fullPath, recursiveImport);
		var importedAny = false;
		foreach (var file in files)
		{
			var skipStartElement = singleRootTag && importedAny;
			ImportFile(file, skipStartElement, skipEndElement: singleRootTag, writer, metadata);
			importedAny = true;
		}

		if (singleRootTag && importedAny)
			writer.WriteEndElement();
	}

	internal static string ApplyCountryOverride(string filePath)
	{
		return ApplyCountryOverride(filePath, GSConfig.SERVER_COUNTRY_CODE);
	}

	internal static string ApplyCountryOverride(string filePath, int countryCode)
	{
		if (!CountryRegions.TryGetValue(countryCode, out var region))
			return filePath;

		var directory = Path.GetDirectoryName(filePath);
		var extension = Path.GetExtension(filePath);
		var fileName = Path.GetFileNameWithoutExtension(filePath);
		var overridePath = Path.Combine(directory ?? string.Empty, $"{fileName}_{region}{extension}");
		return File.Exists(overridePath) ? overridePath : filePath;
	}

	private void ImportFile(string filePath, bool skipStartElement, bool skipEndElement, XmlWriter writer, IDictionary<string, string> metadata)
	{
		metadata[ToMetadataKey(filePath)] = ComputeCrc32(filePath).ToString(CultureInfo.InvariantCulture);
		var settings = new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
		};

		using var reader = XmlReader.Create(filePath, settings);
		XmlQualifiedName? rootElement = null;
		while (reader.Read())
		{
			if (IsIgnorableWhitespace(reader))
				continue;

			if (rootElement == null && reader.NodeType == XmlNodeType.Element)
			{
				rootElement = new XmlQualifiedName(reader.LocalName, reader.NamespaceURI);
				if (skipStartElement)
					continue;
			}

			if (skipEndElement && reader.NodeType == XmlNodeType.EndElement && rootElement != null && reader.LocalName == rootElement.Name && reader.NamespaceURI == rootElement.Namespace)
				continue;

			WriteCurrentNode(reader, writer);
		}
	}

	private List<string> CollectImportedFiles()
	{
		var files = new List<string>();
		var settings = new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
		};

		using var reader = XmlReader.Create(_sourceFilePath, settings);
		while (reader.Read())
		{
			if (reader.NodeType != XmlNodeType.Element || reader.LocalName != ImportElementName)
				continue;

			var importPath = reader.GetAttribute(FileAttributeName);
			if (string.IsNullOrWhiteSpace(importPath))
				throw new XmlException("Attribute 'file' is missing or empty on import element.");

			var fullPath = ApplyCountryOverride(Path.GetFullPath(Path.Combine(_sourceDirectory, importPath)));
			if (File.Exists(fullPath))
			{
				files.Add(fullPath);
				continue;
			}

			if (!Directory.Exists(fullPath))
				throw new FileNotFoundException($"Missing file to import: {fullPath}", fullPath);

			var recursiveImport = bool.Parse(reader.GetAttribute(RecursiveImportAttributeName) ?? "true");
			files.AddRange(ListXmlFiles(fullPath, recursiveImport));
		}

		return files;
	}

	private bool IsCacheCurrent(IReadOnlyList<string> importedFiles)
	{
		// Java parity: reuse merged static-data cache when source CRC metadata is unchanged.
		if (!File.Exists(_cacheFilePath) || !File.Exists(_metadataFilePath))
			return false;

		if (File.GetLastWriteTimeUtc(_sourceFilePath) > File.GetLastWriteTimeUtc(_cacheFilePath))
			return false;

		var metadata = ReadMetadata();
		if (!metadata.TryGetValue(MetadataCountKey, out var count) || count != importedFiles.Count.ToString(CultureInfo.InvariantCulture))
			return false;

		foreach (var file in importedFiles)
		{
			var key = ToMetadataKey(file);
			var crc = ComputeCrc32(file).ToString(CultureInfo.InvariantCulture);
			if (!metadata.TryGetValue(key, out var storedCrc) || storedCrc != crc)
				return false;
		}

		return true;
	}

	private Dictionary<string, string> ReadMetadata()
	{
		var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var line in File.ReadAllLines(_metadataFilePath))
		{
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
				continue;

			var separator = line.IndexOf('|');
			if (separator <= 0)
				continue;

			metadata[line[..separator]] = line[(separator + 1)..];
		}

		return metadata;
	}

	private void WriteMetadata(IReadOnlyDictionary<string, string> metadata)
	{
		using var writer = new StreamWriter(_metadataFilePath);
		writer.WriteLine("# This file is machine-generated. DO NOT EDIT!");
		foreach (var entry in metadata.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
			writer.WriteLine($"{entry.Key}|{entry.Value}");
	}

	private string ToMetadataKey(string filePath)
	{
		return Path.GetRelativePath(_sourceDirectory, filePath).Replace(Path.DirectorySeparatorChar, '/');
	}

	private static IReadOnlyList<string> ListXmlFiles(string directory, bool recursive)
	{
		var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
		return Directory.EnumerateFiles(directory, "*.xml", option)
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.Select(Path.GetFullPath)
			.ToArray();
	}

	private static bool IsIgnorableWhitespace(XmlReader reader)
	{
		return (reader.NodeType is XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace) && string.IsNullOrWhiteSpace(reader.Value);
	}

	private static void WriteCurrentNode(XmlReader reader, XmlWriter writer)
	{
		switch (reader.NodeType)
		{
			case XmlNodeType.Element:
				writer.WriteStartElement(reader.Prefix, reader.LocalName, string.IsNullOrEmpty(reader.NamespaceURI) ? null : reader.NamespaceURI);
				writer.WriteAttributes(reader, defattr: true);
				if (reader.IsEmptyElement)
					writer.WriteEndElement();
				break;
			case XmlNodeType.EndElement:
				writer.WriteEndElement();
				break;
			case XmlNodeType.Text:
				writer.WriteString(reader.Value);
				break;
			case XmlNodeType.CDATA:
				writer.WriteCData(reader.Value);
				break;
		}
	}

	private static uint ComputeCrc32(string filePath)
	{
		var crc = 0xFFFFFFFFu;
		var buffer = new byte[64 * 1024];
		using var stream = File.OpenRead(filePath);
		int read;
		while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
		{
			for (var i = 0; i < read; i++)
				crc = CrcTable[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
		}

		return ~crc;
	}

	private static uint[] BuildCrcTable()
	{
		var table = new uint[256];
		for (uint i = 0; i < table.Length; i++)
		{
			var crc = i;
			for (var j = 0; j < 8; j++)
				crc = (crc & 1) == 1 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
			table[i] = crc;
		}

		return table;
	}
}
