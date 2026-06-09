using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Npcskill;

/// <summary>Java parity: model/templates/npcskill/NpcSkillTargetAttribute (Yeats).</summary>
[XmlType("NpcSkillTargetAttribute")]
public enum NpcSkillTargetAttribute
{
    FRIEND,
    ME,
    MOST_HATED,
    SECOND_MOST_HATED,
    THIRD_MOST_HATED,
    RANDOM,
    RANDOM_EXCEPT_CURRENT_TARGET,
    NONE,
}
