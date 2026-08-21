using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Retail's own skill lists for npcs this port had none for.
/// </summary>
/// <remarks>
/// Retail's <c>&lt;skills&gt;</c> block does two jobs: it is the ordered list <c>SKILLI_INDEX_N</c>
/// points into, and <c>skill_rate</c> marks the skills the npc also casts on its own.
/// <c>SkillAttackManager.ChooseNextSkill</c> prefers a queued skill and falls through to the npc's own
/// list, so an npc with no entry never casts anything a pattern did not ask for.
/// <para>
/// <b>The scale of <c>skill_rate</c> is the whole risk here</b>, and it was measured rather than
/// assumed -- see <c>extract_npc_autonomous_skills.py</c>. A factor-of-ten error gives npcs that
/// either spam their skills or never use them, and neither reads as a bug from a test, so the
/// invariants that would catch one are asserted here against the generated data.
/// </para>
/// </remarks>
public sealed class RetailAutonomousSkillsTests
{
	private static string DataFile() => Path.Combine(BossAiHarness.RepoRoot(),
		"game-server", "data", "static_data", "npc_skills", "retail_autonomous.xml");

	/// <summary><b>No probability escapes the range this port's roll can answer.</b></summary>
	/// <remarks>
	/// <c>ChanceReady</c> is <c>Rnd.Chance() &lt; prob</c> and <c>Rnd.Chance()</c> returns 0-100, so a
	/// prob above 100 is not "more certain", it is a number the roll can never fail -- which is what a
	/// raw retail rate of 1000 or 2000 would become if the per-mille conversion were dropped. That is
	/// the exact shape of the mistake this file is most likely to make, and it would look like npcs
	/// casting constantly rather than like a parsing error.
	/// </remarks>
	[Fact]
	public void EveryProbabilityIsAPercentage()
	{
		string xml = File.ReadAllText(DataFile());
		int[] probs = Regex.Matches(xml, "prob=\"([0-9]+)\"")
			.Select(m => int.Parse(m.Groups[1].Value)).ToArray();

		Assert.NotEmpty(probs);
		Assert.All(probs, p => Assert.InRange(p, 0, 100));

		// And the conversion is not collapsing everything to zero either, which is the opposite
		// failure and just as quiet: npcs that have skills and never use them.
		Assert.True(probs.Count(p => p > 0) > 10000,
			$"only {probs.Count(p => p > 0)} skills are castable at all, so the rate conversion has "
			+ "flattened retail's list into one nothing chooses");
	}

	/// <summary><b>This file never speaks for an npc the port already had tuning for.</b></summary>
	/// <remarks>
	/// The port's existing <c>npc_skills</c> entries carry aionemu's own numbers. Overwriting them
	/// would trade one source for another in encounters nobody asked about, and because both files
	/// load into the same list the duplicate would not error -- the npc would simply end up with the
	/// skill twice at two different probabilities.
	/// </remarks>
	[Fact]
	public void ItAddsNpcsRatherThanOverwritingThem()
	{
		string directory = Path.GetDirectoryName(DataFile())!;
		var mine = Owners(File.ReadAllText(DataFile()));
		Assert.NotEmpty(mine);

		foreach (string other in Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories))
		{
			if (string.Equals(other, DataFile(), StringComparison.OrdinalIgnoreCase))
				continue;

			var theirs = Owners(File.ReadAllText(other));
			var shared = mine.Intersect(theirs).Take(5).ToArray();
			Assert.True(shared.Length == 0,
				$"{Path.GetFileName(other)} already speaks for {string.Join(", ", shared)}");
		}
	}

	/// <summary>Every npc id an <c>npc_ids</c> attribute names.</summary>
	/// <remarks>
	/// Split on any whitespace, not on spaces: the existing files separate ids with tabs as well, and
	/// a space-only split turns "236231	236234" into one unparseable token. Found by this pin.
	/// </remarks>
	private static int[] Owners(string xml) =>
		Regex.Matches(xml, "npc_ids=\"([^\"]+)\"")
			.SelectMany(m => Regex.Split(m.Groups[1].Value, @"\s+"))
			.Where(id => id.Length > 0)
			.Select(int.Parse).ToArray();
}
