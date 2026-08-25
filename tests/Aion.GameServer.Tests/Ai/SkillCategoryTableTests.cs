using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.SkillEngine.Model;
using Xunit;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// <b>Retail's skill categories, which this port had no source for.</b>
/// </summary>
/// <remarks>
/// <c>is_event_skill_category</c> on <c>on_friend_spelled</c> is 11 patterns and <b>147 npcs</b> — a
/// support npc watching for its friend to be debuffed or healed. It could not be read at all, because
/// nothing here knew a skill's category.
/// <para>
/// <b>The negative pin is the point.</b> A lookup that answers <c>NONE</c> for everything and a lookup
/// that answers the same category for everything both leave a positive pin passing. Only asking for a
/// skill in a different category, and one in no category, distinguishes them.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public class SkillCategoryTableTests
{
	/// <summary>The four categories retail's AI actually asks about.</summary>
	private static readonly SkillCategory[] Asked =
	[
		SkillCategory.PHYSICAL_DEBUFF, SkillCategory.MENTAL_DEBUFF,
		SkillCategory.HEAL, SkillCategory.CHAIN_SKILL,
	];

	[Fact]
	public void TheTableLoadsAtTheSizeRetailGives()
	{
		StaticTableFixture.EnsureLoaded();

		// Retail names a category for 2,052 of its 14,393 skills; the other 12,341 are SKILLCTG_NONE
		// and are deliberately absent, because "not listed" and "no category" have to mean the same
		// thing.
		//
		// 1,977 of those 2,052 survive here. **The dump is 5.8 and this port is 4.8**, so 75 name a
		// skill with no template in this tree and the extractor drops them rather than carrying 5.8
		// content into a 4.8 file. That number is expected to move if the port's skill data does.
		Assert.Equal(1977, DataManager.SKILL_CATEGORY_DATA.Size);
	}

	/// <summary>Every category the AI asks about has skills in it, so no branch is dead on arrival.</summary>
	[Fact]
	public void EveryCategoryTheAiAsksAboutHasSkills()
	{
		StaticTableFixture.EnsureLoaded();

		foreach (SkillCategory category in Asked)
		{
			Assert.True(
				Enumerable.Range(1, 60000).Any(id => DataManager.SKILL_CATEGORY_DATA.Of(id) == category),
				$"no skill is in {category}, so every branch asking for it is dead");
		}
	}

	/// <summary>
	/// <b>A skill in no category answers <c>NONE</c>, not the nearest thing.</b> Skill 1 is retail's
	/// <c>RA_Light_WhiteTiger_G1</c>, whose category is <c>SKILLCTG_NONE</c>.
	/// </summary>
	[Fact]
	public void ASkillWithNoCategoryAnswersNone()
	{
		StaticTableFixture.EnsureLoaded();

		Assert.Equal(SkillCategory.NONE, DataManager.SKILL_CATEGORY_DATA.Of(1));

		// And an id no skill has at all, which is the same statement from the other side.
		Assert.Equal(SkillCategory.NONE, DataManager.SKILL_CATEGORY_DATA.Of(999_999));
	}

	/// <summary>
	/// <b>The categories are not this port's own skill attributes wearing a new name.</b> Retail's
	/// <c>PHYSICAL_DEBUFF</c> is mostly <c>skilltype="MAGICAL"</c> here, which is why the field is
	/// ported rather than derived — see <see cref="SkillCategoryData"/>.
	/// </summary>
	/// <remarks>
	/// Pinned because the cheap-looking alternative is to map <c>skilltype</c>/<c>skillsubtype</c> onto
	/// these four names, and it reads as reasonable right up until somebody measures it.
	/// </remarks>
	[Fact]
	public void PhysicalDebuffIsMostlyNotThisPortsPhysicalSkillType()
	{
		StaticTableFixture.EnsureLoaded();

		// Read straight from the file rather than through SkillData: this fixture registers the AI
		// tables only, and pulling the whole skill engine in for one attribute would be a heavier
		// fixture for a smaller claim. The sibling pins read their tables the same way.
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"game-server", "data", "static_data", "skills", "skill_templates.xml");
		var skillType = new Dictionary<int, string>();
		foreach (Match m in Regex.Matches(File.ReadAllText(path),
			@"<skill_template\s+skill_id=""(\d+)""[^>]*?skilltype=""(\w+)"""))
		{
			skillType[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value;
		}

		int magical = 0;
		int physical = 0;
		foreach ((int id, string type) in skillType)
		{
			if (DataManager.SKILL_CATEGORY_DATA.Of(id) != SkillCategory.PHYSICAL_DEBUFF)
				continue;
			if (type == "MAGICAL")
				magical++;
			else if (type == "PHYSICAL")
				physical++;
		}

		Assert.True(magical > 0 && physical > 0, "read no PHYSICAL_DEBUFF skills at all");
		Assert.True(magical > physical,
			$"PHYSICAL_DEBUFF: {magical} magical vs {physical} physical — if this ever flips, the "
			+ "derive-it-from-skilltype shortcut deserves another look");
	}
}
