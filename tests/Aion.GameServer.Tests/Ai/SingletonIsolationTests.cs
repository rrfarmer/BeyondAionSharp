using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// One structural pin: every test class that swaps a global singleton must be in the single
/// serialising collection.
/// </summary>
/// <remarks>
/// <b>Written because a missing attribute cost three wrong diagnoses.</b> <c>DataManager</c>,
/// <c>GameWorld</c> and <c>ThreadPoolManager</c> are process-wide singletons with a
/// <c>RegisterInstance</c> hatch, and several test classes swap them for a fixture and put them back
/// afterwards. xUnit runs <em>different collections</em> in parallel — a class in a private collection
/// with <c>DisableParallelization</c> is serialised against itself and against nothing else — so any
/// two such classes in different collections will occasionally pull the world out from under each
/// other.
/// <para>
/// From the outside that looks like an unrelated pin failing about one full-suite run in seven, with
/// no random branch anywhere on its own path, passing every time it is run alone. Three explanations
/// were tried before the cause was found: a probabilistic window, a race against an aggro scan, and
/// one harness class missing the attribute. <b>The first two were wrong and the third was a real bug
/// that was not this one.</b> The culprit was a class with its own private collection.
/// </para>
/// <para>
/// <b>This is a rule a reviewer cannot see and a test can.</b> The attribute's absence looks like
/// nothing: the class compiles and it passes on its own every time.
/// </para>
/// <para>
/// Source-scanning rather than reflection, because the thing to look for is a <em>call</em> and
/// reflection cannot read method bodies. See docs/retail-ai-fidelity.md.
/// </para>
/// </remarks>
public sealed class SingletonIsolationTests
{
	/// <summary>The collection whose whole purpose is to stop these classes running side by side.</summary>
	private const string Serialising = "GoldenDataManager";

	/// <summary>The attribute, in either spelling the project uses.</summary>
	private static readonly Regex InTheCollection = new(
		@"\[\s*(?:Xunit\.)?Collection\s*\(\s*""GoldenDataManager""\s*\)\s*\]",
		RegexOptions.Compiled);

	/// <summary>What a test file does that makes it unsafe beside another one.</summary>
	private static readonly Regex SwapsASingleton = new(
		@"\b(DataManager|GameWorld|ThreadPoolManager)\.(Register|Restore)Instance\s*\(",
		RegexOptions.Compiled);

	[Fact]
	public void EveryTestClassThatSwapsAGlobalSingletonIsInTheSerialisingCollection()
	{
		var loose = new List<string>();

		foreach (string path in Directory.EnumerateFiles(TestSourceRoot(), "*.cs", SearchOption.AllDirectories))
		{
			// The collection definition and the harness are the machinery, not users of it.
			string name = Path.GetFileName(path);
			if (name is "GoldenDataManagerCollection.cs" or "BossAiHarness.cs" or "SingletonIsolationTests.cs")
				continue;

			string text = File.ReadAllText(path);
			if (!SwapsASingleton.IsMatch(text))
				continue;

			// Both spellings are in use: [Collection("...")] and [Xunit.Collection("...")].
			if (!InTheCollection.IsMatch(text))
				loose.Add(name);
		}

		Assert.True(loose.Count == 0,
			"these swap DataManager, GameWorld or ThreadPoolManager and are not in the \"" + Serialising
			+ "\" collection, so they run in parallel with every test that shares those singletons:"
			+ Environment.NewLine + string.Join(Environment.NewLine, loose.OrderBy(s => s)));
	}

	/// <summary>The test project's source directory, walked up from the built assembly.</summary>
	/// <remarks>
	/// Looks for the csproj rather than assuming a fixed number of levels, so a change of target
	/// framework or configuration cannot silently turn this pin into a no-op over an empty directory —
	/// which is the usual way a source-scanning test stops working without anyone noticing.
	/// </remarks>
	private static string TestSourceRoot()
	{
		DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null && !dir.EnumerateFiles("Aion.GameServer.Tests.csproj").Any())
			dir = dir.Parent;

		Assert.True(dir != null,
			"could not find Aion.GameServer.Tests.csproj above " + AppContext.BaseDirectory);
		return dir!.FullName;
	}
}
