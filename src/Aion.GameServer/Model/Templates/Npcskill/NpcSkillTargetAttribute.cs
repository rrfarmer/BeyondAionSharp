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

    /// <summary>Retail's <c>ATTACKERI_HAS_LOWEST_HP</c> and <c>ATTACKERI_HAS_MOST_HP</c> as a skill target.</summary>
    /// <remarks>
    /// Added for the retail AI patterns rather than for Java, which names neither -- the same reason
    /// and the same pair already added to <see cref="Controllers.Attack.AggroTarget"/>, and resolved by
    /// delegating to it, so the two agree by construction rather than by a second implementation.
    /// <para>
    /// Without these a boss could <b>switch</b> to whoever is closest to dying but not <b>cast</b> at
    /// them, which left 235 retail uses unportable. Picking on the one most nearly dead is the fourth
    /// most common thing a boss does with a target.
    /// </para>
    /// <para>
    /// <b>Appended deliberately.</b> These are read from <c>npc_skills.xml</c> by name, but Java
    /// compares this enum by <c>ordinal()</c> in places and C# by its integer value; adding anywhere
    /// but the end would renumber the members that already exist. See CLAUDE.md on enum ordinals.
    /// </para>
    /// </remarks>
    LOWEST_HP,
    MOST_HP,
}
