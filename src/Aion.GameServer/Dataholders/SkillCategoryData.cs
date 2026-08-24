using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Dataholders;

/// <summary>Retail's skill categories, the ones its AI asks about.</summary>
/// <remarks>
/// Retail's <c>is_event_skill_category</c> on <c>on_friend_spelled</c> asks whether the skill just cast
/// on a friend was a physical debuff, a mental one, or a heal. Eleven patterns ask it and 147 npcs run
/// them, and the condition could not be read because nothing here knew a skill's category.
/// <para>
/// <b>It is not derivable from <c>skill_templates.xml</c>, and not by a small margin.</b> Retail's
/// <c>PHYSICAL_DEBUFF</c> is mostly <c>skilltype="MAGICAL"</c> in this port's own data, and the
/// <c>MAGICAL</c>/<c>DEBUFF</c> signature covers 1,382 skills here of which 1,248 have no retail
/// category at all. Deriving it would be wrong for the overwhelming majority. Retail names the field
/// outright, so it is ported.
/// </para>
/// <para>
/// <b>This has no Java counterpart</b>, like <see cref="GuardAnswerData"/>: aionemu has nothing
/// equivalent. The file is generated; the extractor owns it and hand edits are lost on the next run.
/// </para>
/// </remarks>
[XmlRoot("skill_categories")]
public class SkillCategoryData
{
    [XmlElement("category")] public List<SkillCategorySet>? categories;

    [XmlIgnore] private readonly Dictionary<int, SkillCategory> bySkill = new();

    /// <summary>How many skills carry a category.</summary>
    [XmlIgnore] public int Size => bySkill.Count;

    /// <summary>
    /// The category retail gives this skill, or <see cref="SkillCategory.NONE"/> when it gives none.
    /// </summary>
    /// <remarks>
    /// An absent skill answers <c>NONE</c> rather than throwing: retail writes <c>SKILLCTG_NONE</c> for
    /// 12,341 of its 14,393 records and the emitter drops them, so "not in the table" and "retail says
    /// no category" are the same statement and have to answer the same way.
    /// </remarks>
    public SkillCategory Of(int skillId)
        => bySkill.TryGetValue(skillId, out SkillCategory category) ? category : SkillCategory.NONE;

    public void AfterUnmarshal(object parent)
    {
        bySkill.Clear();
        foreach (SkillCategorySet set in categories ?? [])
        {
            if (!Enum.TryParse(set.name, out SkillCategory category))
            {
                // A category this port does not name. Refused loudly rather than dropped: the file is
                // generated from retail's own field, so a name that does not parse means the two
                // vocabularies have drifted and every branch asking for it would silently answer no.
                throw new ArgumentException(
                    $"retail_skill_categories.xml names category '{set.name}', "
                    + "which SkillCategory does not have");
            }

            foreach (string line in set.skills ?? [])
            {
                foreach (string id in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    bySkill[int.Parse(id)] = category;
                }
            }
        }

        categories = null;
    }
}

/// <summary>One category and the skills in it.</summary>
public class SkillCategorySet
{
    [XmlAttribute("name")] public string? name;

    [XmlAttribute("count")] public int count;

    /// <summary>Ids, whitespace-separated, sixteen to a line. See the emitter for why.</summary>
    [XmlElement("skills")] public List<string>? skills;
}
