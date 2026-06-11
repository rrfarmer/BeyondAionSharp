using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// Skills required to use/equip an item.
/// Java parity: model/templates/item/RequireSkill (@XmlType("RequireSkill")).
/// </summary>
/// <remarks>Java's @XmlAttribute List&lt;Integer&gt; serializes as a space-separated string.</remarks>
[XmlType("RequireSkill")]
public class RequireSkill
{
    [XmlAttribute("skillIds")] public string? SkillIdsRaw { get; set; }
    private List<int>? _skillIds;

    // Java parity: getSkillIds() — lazily initialized list (parsed from the space-separated attribute).
    public List<int> GetSkillIds()
    {
        if (_skillIds == null)
            _skillIds = string.IsNullOrWhiteSpace(SkillIdsRaw)
                ? new List<int>()
                : SkillIdsRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        return _skillIds;
    }
}
