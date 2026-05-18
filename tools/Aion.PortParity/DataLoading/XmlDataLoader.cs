using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Aion.PortParity.DataLoading
{
	/// <summary>
	/// Loads and validates XML static data files.
	/// Compares element counts and structure against Java to ensure parity.
	/// </summary>
	public class XmlDataLoader
	{
		/// <summary>
		/// Load an XML file and return element count statistics.
		/// </summary>
		public static XmlStatistics LoadXmlStatistics(string filePath)
		{
			if (!File.Exists(filePath))
				throw new FileNotFoundException($"XML file not found: {filePath}");

			var doc = XDocument.Load(filePath);
			var stats = new XmlStatistics
			{
				FilePath = filePath,
				FileName = Path.GetFileName(filePath),
				RootElementName = doc.Root?.Name.LocalName ?? "unknown",
			};

			// Count all elements recursively
			CountElements(doc.Root, stats);

			return stats;
		}

		/// <summary>
		/// Load all XML files from a directory matching a pattern.
		/// Returns aggregated statistics.
		/// </summary>
		public static Dictionary<string, XmlStatistics> LoadDirectoryStatistics(string dirPath, string searchPattern = "*.xml", bool recursive = true)
		{
			if (!Directory.Exists(dirPath))
				throw new DirectoryNotFoundException($"Directory not found: {dirPath}");

			var results = new Dictionary<string, XmlStatistics>();
			var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

			foreach (var file in Directory.GetFiles(dirPath, searchPattern, searchOption))
			{
				try
				{
					var stats = LoadXmlStatistics(file);
					var relativePath = Path.GetRelativePath(dirPath, file);
					results[relativePath] = stats;
				}
				catch (Exception ex)
				{
					results[Path.GetRelativePath(dirPath, file)] = new XmlStatistics
					{
						FilePath = file,
						FileName = Path.GetFileName(file),
						Error = ex.Message,
					};
				}
			}

			return results;
		}

		private static void CountElements(XElement? element, XmlStatistics stats)
		{
			if (element == null)
				return;

			var elementName = element.Name.LocalName;

			if (!stats.ElementCounts.ContainsKey(elementName))
				stats.ElementCounts[elementName] = 0;

			stats.ElementCounts[elementName]++;
			stats.TotalElements++;

			// Count attributes
			foreach (var attr in element.Attributes())
			{
				stats.TotalAttributes++;
			}

			// Recurse into children
			foreach (var child in element.Elements())
			{
				CountElements(child, stats);
			}
		}

		/// <summary>
		/// Compare statistics from two XML files and report differences.
		/// </summary>
		public static XmlComparisonResult CompareStatistics(XmlStatistics java, XmlStatistics csharp)
		{
			var result = new XmlComparisonResult
			{
				JavaFile = java.FilePath,
				CSharpFile = csharp.FilePath,
				Match = true,
			};

			// Compare root element
			if (java.RootElementName != csharp.RootElementName)
			{
				result.Match = false;
				result.Differences.Add($"Root element mismatch: Java='{java.RootElementName}' vs C#='{csharp.RootElementName}'");
			}

			// Compare total element count
			if (java.TotalElements != csharp.TotalElements)
			{
				result.Match = false;
				result.Differences.Add($"Total element count mismatch: Java={java.TotalElements} vs C#={csharp.TotalElements}");
			}

			// Compare individual element types
			var allKeys = new HashSet<string>(java.ElementCounts.Keys);
			allKeys.UnionWith(csharp.ElementCounts.Keys);

			foreach (var key in allKeys.OrderBy(k => k))
			{
				var javaCount = java.ElementCounts.TryGetValue(key, out var jc) ? jc : 0;
				var csharpCount = csharp.ElementCounts.TryGetValue(key, out var cc) ? cc : 0;

				if (javaCount != csharpCount)
				{
					result.Match = false;
					result.ElementDifferences[key] = new ElementDifference { JavaCount = javaCount, CSharpCount = csharpCount };
				}
			}

			return result;
		}
	}

	/// <summary>
	/// Statistics about an XML file's content.
	/// </summary>
	public class XmlStatistics
	{
		public string FilePath { get; set; } = "";
		public string FileName { get; set; } = "";
		public string RootElementName { get; set; } = "";
		public Dictionary<string, int> ElementCounts { get; set; } = new();
		public int TotalElements { get; set; }
		public int TotalAttributes { get; set; }
		public string? Error { get; set; }

		public override string ToString()
		{
			if (!string.IsNullOrEmpty(Error))
				return $"{FileName}: ERROR - {Error}";

			var details = string.Join(", ", ElementCounts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"));

			return $"{FileName} (root={RootElementName}, total={TotalElements} elements, {TotalAttributes} attributes) [{details}]";
		}
	}

	/// <summary>
	/// Result of comparing two XML statistics.
	/// </summary>
	public class XmlComparisonResult
	{
		public string JavaFile { get; set; } = "";
		public string CSharpFile { get; set; } = "";
		public bool Match { get; set; }
		public List<string> Differences { get; set; } = new();
		public Dictionary<string, ElementDifference> ElementDifferences { get; set; } = new();

		public override string ToString()
		{
			var status = Match ? "✓ MATCH" : "✗ MISMATCH";
			var summary = $"{status}: {Path.GetFileName(JavaFile)} vs {Path.GetFileName(CSharpFile)}";

			if (Differences.Count == 0)
				return summary;

			return summary + "\n  " + string.Join("\n  ", Differences);
		}
	}

	/// <summary>
	/// Difference in element count between Java and C# versions.
	/// </summary>
	public class ElementDifference
	{
		public int JavaCount { get; set; }
		public int CSharpCount { get; set; }
	}
}
