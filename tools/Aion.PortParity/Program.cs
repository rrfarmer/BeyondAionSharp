using System;
using System.IO;
using Aion.PortParity.DataLoading;

namespace Aion.PortParity
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				PrintUsage();
				return;
			}

			var command = args[0].ToLower();

			try
			{
				switch (command)
				{
					case "xml":
						CompareXmlCommand(args);
						break;
					case "help":
						PrintUsage();
						break;
					default:
						Console.WriteLine($"Unknown command: {command}");
						PrintUsage();
						break;
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"Error: {ex.Message}");
				Environment.Exit(1);
			}
		}

		static void CompareXmlCommand(string[] args)
		{
			if (args.Length < 3)
			{
				Console.WriteLine("Usage: Aion.PortParity xml <java-data-dir> <csharp-data-dir>");
				Console.WriteLine("  Compares XML element counts between Java and C# static data directories.");
				return;
			}

			var javaDir = args[1];
			var csharpDir = args[2];

			if (!Directory.Exists(javaDir))
			{
				Console.Error.WriteLine($"Java data directory not found: {javaDir}");
				return;
			}

			if (!Directory.Exists(csharpDir))
			{
				Console.Error.WriteLine($"C# data directory not found: {csharpDir}");
				return;
			}

			Console.WriteLine($"Loading Java XML from: {javaDir}");
			var javaStats = XmlDataLoader.LoadDirectoryStatistics(javaDir);
			Console.WriteLine($"  Loaded {javaStats.Count} XML files\n");

			Console.WriteLine($"Loading C# XML from: {csharpDir}");
			var csharpStats = XmlDataLoader.LoadDirectoryStatistics(csharpDir);
			Console.WriteLine($"  Loaded {csharpStats.Count} XML files\n");

			Console.WriteLine("Comparison Results:");
			Console.WriteLine("===================");

			var matchCount = 0;
			var mismatchCount = 0;
			var totalElements = 0;

			foreach (var javaFile in javaStats.Keys.OrderBy(k => k))
			{
				var csharpFile = csharpStats.Keys.FirstOrDefault(k => k.EndsWith(Path.GetFileName(javaFile)));

				if (csharpFile == null)
				{
					Console.WriteLine($"⚠ Missing in C#: {javaFile}");
					mismatchCount++;
					continue;
				}

				var javaXml = javaStats[javaFile];
				var csharpXml = csharpStats[csharpFile];

				if (javaXml.Error != null || csharpXml.Error != null)
				{
					Console.WriteLine($"⚠ Error loading: {javaFile}");
					if (javaXml.Error != null)
						Console.WriteLine($"   Java: {javaXml.Error}");
					if (csharpXml.Error != null)
						Console.WriteLine($"   C#: {csharpXml.Error}");
					mismatchCount++;
					continue;
				}

				var comparison = XmlDataLoader.CompareStatistics(javaXml, csharpXml);

				if (comparison.Match)
				{
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine($"✓ {Path.GetFileName(javaFile)}: {javaXml.TotalElements} elements");
					Console.ResetColor();
					matchCount++;
					totalElements += javaXml.TotalElements;
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"✗ {Path.GetFileName(javaFile)}: MISMATCH");
					Console.ResetColor();
					foreach (var diff in comparison.Differences)
					{
						Console.WriteLine($"   {diff}");
					}
					foreach (var elemDiff in comparison.ElementDifferences)
					{
						Console.WriteLine($"   {elemDiff.Key}: Java={elemDiff.Value.JavaCount}, C#={elemDiff.Value.CSharpCount}");
					}
					mismatchCount++;
				}
			}

			Console.WriteLine("\nSummary:");
			Console.WriteLine($"  Matched: {matchCount}/{javaStats.Count}");
			Console.WriteLine($"  Mismatched: {mismatchCount}/{javaStats.Count}");
			Console.WriteLine($"  Total Elements: {totalElements}");

			if (mismatchCount > 0)
				Environment.Exit(1);
		}

		static void PrintUsage()
		{
			Console.WriteLine("Aion Port Parity Tool - Validate Java/C# conversion parity");
			Console.WriteLine("");
			Console.WriteLine("Usage: Aion.PortParity <command> [options]");
			Console.WriteLine("");
			Console.WriteLine("Commands:");
			Console.WriteLine("  xml <java-dir> <csharp-dir>    Compare XML element counts");
			Console.WriteLine("  help                           Show this help message");
			Console.WriteLine("");
			Console.WriteLine("Examples:");
			Console.WriteLine("  Aion.PortParity xml ../../game-server/data/static_data ../Aion.GameServer/data/static_data");
		}
	}
}
